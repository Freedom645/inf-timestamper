using System.Text;
using System.Text.Json;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.DakenCounter;

/// <summary>
/// 打鍵カウンタ（inf_daken_counter_obsw）のデータ配信 WebSocket（既定 <c>ws://127.0.0.1:8767</c>）を購読し、
/// INFINITAS のプレイ開始 / プレイリザルトを検知する（issue #13。Reflux の代わりに選べる検知元）。
///
/// 打鍵カウンタの配信には<b>プレイ開始を示すメッセージが無い</b>。画面遷移のログもコメントアウトされていて使えない。
/// そこで実機ダンプ（docs/sample/daken-counter/probe.jsonl。Reflux の記録と並べて取得）で確かめた次の性質から、
/// プレイを<b>終わった時点でさかのぼって</b>確定させる。
/// <list type="bullet">
/// <item><description>
/// <c>graph</c> はプレイ終了時（判定数 &gt; 0 のとき）に飛び、<c>playcount</c> が 1 増える。
/// 打鍵カウンタの起動時・設定変更時にも飛ぶが、そのときは <c>playcount</c> が変わらない。
/// → <c>playcount</c> の増加を<b>プレイ終了</b>とする。
/// </description></item>
/// <item><description>
/// プレイ中は何も飛ばない（選曲画面・オプション画面・リザルト画面では画面の読み取り結果が飛ぶ）。
/// → プレイ終了の直前に受信した時刻を<b>プレイ開始</b>とする。実機では Reflux のプレイ開始より
/// 0〜8 秒早い（曲決定画面〜ロード中の分）。クイックリトライでは直前のアテンプトの終了時刻になる。
/// </description></item>
/// <item><description>
/// <c>today_updates</c> はリザルト登録時（プレイ終了の 3〜8 秒後）に飛ぶ。ただし BPIM2（BPI）を
/// 非同期に取りに行った結果が返るたびに、同じ内容が <c>history_cursong</c> と組で再送される。
/// 再送は画面と無関係に届き、プレイ中にも届くので、プレイ開始の判定からは外す
/// （外さないと、リザルト画面から即リトライしたときに開始時刻が最大 3 秒遅れる）。
/// </description></item>
/// </list>
/// 接続失敗・切断は指数バックオフで再接続し、配信の記録自体には影響させない（ログのみ）。
/// </summary>
public sealed class DakenCounterPlayWatcher : IPlayWatcher
{
    /// <summary>
    /// BPIM2 の再送は <c>history_cursong</c> → <c>today_updates</c> の順に数十ミリ秒差で届く
    /// （実機で 50ms 前後）。重複した <c>today_updates</c> の直前この時間内の受信は再送の一部とみなす。
    /// </summary>
    private static readonly TimeSpan ResendBurstWindow = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// <c>today_updates</c> のエントリ時刻（秒精度。リザルト画面に入った時刻）がプレイ開始より前なら、
    /// 前のプレイのものとみなして捨てる。丸めで手前に出るぶんだけ猶予を持たせる。
    /// </summary>
    private static readonly TimeSpan ResultTimeSlack = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 1 メッセージの上限。<c>history_cursong</c> は譜面ごとの過去のプレイ履歴をまるごと含むので大きくなりうる
    /// （実機で最大 50KB 程度）。
    /// </summary>
    private const int MaxMessageBytes = 4 * 1024 * 1024;

    /// <summary>プレイ開始の判定に使う受信時刻を、再送の巻き戻し用にいくつか遡れるだけ保持する。</summary>
    private const int MaxRecentFeeds = 32;

    private readonly ILogger<DakenCounterPlayWatcher> _logger;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>接続した時刻。受信が 1 件も無いときのプレイ開始の下限。</summary>
    private DateTimeOffset? _connectedAt;

    /// <summary>プレイ開始の判定に使う、画面由来のメッセージの受信時刻（古い順）。</summary>
    private readonly List<DateTimeOffset> _recentFeeds = new();

    /// <summary>接続後に最初に見た <c>playcount</c>。これより増えたらプレイ終了。接続のたびに取り直す。</summary>
    private int? _playcount;

    /// <summary>直近の <c>history_cursong</c> から取った曲情報。プレイ開始時に添える。</summary>
    private IReadOnlyDictionary<string, string>? _currentSong;

    /// <summary>リザルト待ちのプレイの開始時刻。</summary>
    private DateTimeOffset? _awaitingResultSince;

    /// <summary>見たことのあるリザルトの内容。BPIM2 の再送と、接続直後に再送される最新分を弾く。</summary>
    private readonly HashSet<string> _seenResults = new(StringComparer.Ordinal);

    private bool _disposed;

    public DakenCounterPlayWatcher()
        : this(NullLogger<DakenCounterPlayWatcher>.Instance) { }

    public DakenCounterPlayWatcher(ILogger<DakenCounterPlayWatcher> logger)
    {
        _logger = logger ?? NullLogger<DakenCounterPlayWatcher>.Instance;
    }

    /// <summary>
    /// プレイ開始検知。プレイ終了時に発火し、CapturedAt はさかのぼったプレイ開始時刻。
    /// Fields は直前に選曲画面・リザルト画面で見えていた譜面の曲情報（リザルトで上書きされる）。
    /// </summary>
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は today_updates の最新エントリから展開した各識別子。</summary>
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public bool IsRunning
    {
        get { lock (_gate) return _cts is not null; }
    }

    /// <summary><see cref="WatchTarget.Endpoint"/> は <c>ws://host:port</c> 形式の接続先（必須）。</summary>
    public void Start(WatchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Start(target.Endpoint ?? string.Empty);
    }

    /// <summary><paramref name="source"/> は <c>ws://host:port</c> 形式の接続先。</summary>
    public void Start(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("打鍵カウンタの接続先が指定されていません。", nameof(source));

        if (!WebSocketSubscription.TryParseEndpoint(source, out var endpoint))
            throw new ArgumentException($"打鍵カウンタの接続先が不正です: {source}", nameof(source));

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DakenCounterPlayWatcher));
            if (_cts is not null)
                throw new InvalidOperationException("既に監視が動作中です。");

            ResetState();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            var subscription = new WebSocketSubscription(
                "打鍵カウンタ", MaxMessageBytes, _logger,
                onConnected: () => OnConnected(DateTimeOffset.Now),
                onMessage: message => HandleMessage(message, DateTimeOffset.Now));
            _loopTask = Task.Run(() => subscription.RunAsync(endpoint, token), token);

            _logger.LogInformation("打鍵カウンタへの接続を開始します: {Endpoint}", endpoint);
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loopTask;

        lock (_gate)
        {
            if (_cts is null) return;
            cts = _cts;
            loopTask = _loopTask;
            _cts = null;
            _loopTask = null;
            ResetState();
        }

        try { cts.Cancel(); } catch (ObjectDisposedException) { /* ignore */ }

        if (loopTask is not null)
        {
            try { await loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* 想定内 */ }
            catch (Exception ex) { _logger.LogDebug(ex, "打鍵カウンタの受信ループの終了時に例外が発生しました。"); }
        }

        cts.Dispose();
        _logger.LogInformation("打鍵カウンタへの接続を停止しました。");
    }

    private void ResetState()
    {
        _connectedAt = null;
        _recentFeeds.Clear();
        _playcount = null;
        _currentSong = null;
        _awaitingResultSince = null;
        _seenResults.Clear();
    }

    /// <summary>
    /// 接続が確立したとき。接続直後にはキャッシュ済みの最新データがまとめて再送されてくるので、
    /// <c>playcount</c> の基準はそこで取り直す（切断中のプレイは拾えない）。
    /// リザルト待ちと見たことのあるリザルトは引き継ぐ（切断中に登録されたリザルトを再接続後に拾える）。
    /// </summary>
    internal void OnConnected(DateTimeOffset at)
    {
        lock (_gate)
        {
            _connectedAt = at;
            _recentFeeds.Clear();
            _playcount = null;
        }
    }

    /// <summary>受信した 1 メッセージ（UTF-8 の JSON）を処理する。テストからも直接呼ぶ。</summary>
    internal void HandleMessage(ReadOnlyMemory<byte> utf8Json, DateTimeOffset receivedAt)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "打鍵カウンタからのメッセージを解析できませんでした。");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return;
            if (!root.TryGetProperty("data", out var data)) return;

            if (type.ValueEquals("graph"))
                ProcessGraph(data, receivedAt);
            else if (type.ValueEquals("today_updates"))
                ProcessTodayUpdates(data, receivedAt);
            else if (type.ValueEquals("history_cursong"))
                ProcessCurrentSong(data, receivedAt);
            else if (type.ValueEquals("option"))
                AddFeed(receivedAt);
            // today_stats は graph / today_updates に 1 秒ほど遅れて必ず続くだけなので、判定に使わない
        }
    }

    /// <summary>テスト用: 文字列で渡す。</summary>
    internal void HandleMessage(string json, DateTimeOffset receivedAt)
        => HandleMessage(Encoding.UTF8.GetBytes(json), receivedAt);

    /// <summary><c>graph</c>: <c>playcount</c> が増えていたらプレイ終了。開始時刻をさかのぼって確定する。</summary>
    private void ProcessGraph(JsonElement data, DateTimeOffset receivedAt)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("playcount", out var raw)
            || raw.ValueKind != JsonValueKind.Number
            || !raw.TryGetInt32(out var playcount))
        {
            return;
        }

        DateTimeOffset startedAt;
        IReadOnlyDictionary<string, string> fields;
        lock (_gate)
        {
            var previous = _playcount;
            _playcount = playcount;

            if (previous is null || playcount <= previous)
            {
                // 接続直後の基準取り / 設定変更などでの再送（playcount 据え置き）/ 日付が変わってのリセット
                if (previous is not null && playcount < previous)
                    _logger.LogInformation("打鍵カウンタ: プレイ回数が減ったため（{Previous} → {Current}）、基準を取り直します。",
                        previous, playcount);
                AddFeedLocked(receivedAt);
                return;
            }

            if (playcount - previous > 1)
                _logger.LogWarning("打鍵カウンタ: プレイ回数が 2 以上増えました（{Previous} → {Current}）。最後の 1 プレイだけを記録します。",
                    previous, playcount);

            startedAt = LastFeedLocked() ?? receivedAt;
            fields = _currentSong ?? new Dictionary<string, string>();
            _awaitingResultSince = startedAt;
            AddFeedLocked(receivedAt);
        }

        _logger.LogInformation(
            "打鍵カウンタ: プレイ終了を検知しました。プレイ開始を記録します（{At:HH:mm:ss}、{Elapsed:F0} 秒前、曲: {Title}）。",
            startedAt, (receivedAt - startedAt).TotalSeconds, Describe(fields, FieldKeys.Title));
        PlayStarted?.Invoke(this, new PlayStartedEventArgs(startedAt, fields));
    }

    /// <summary><c>today_updates</c>: 新しく登録されたリザルトを、リザルト待ちのプレイにだけ採る。</summary>
    private void ProcessTodayUpdates(JsonElement data, DateTimeOffset receivedAt)
    {
        if (!TryGetLatestResult(data, out var latest, out var resultTime))
        {
            AddFeed(receivedAt);
            return;
        }

        var key = BuildResultKey(latest);
        DateTimeOffset awaitingSince;
        lock (_gate)
        {
            if (!_seenResults.Add(key))
            {
                // BPIM2 の取得完了による再送（直前の history_cursong と組で届く）。画面と無関係なので巻き戻す
                RollBackResendLocked(receivedAt);
                return;
            }

            AddFeedLocked(receivedAt);

            if (_awaitingResultSince is null)
            {
                // 接続直後に再送された最新分、選曲画面から自己ベストを取り込んだ分など
                _logger.LogDebug("打鍵カウンタ: リザルト待ちでないため today_updates を無視します。");
                return;
            }
            awaitingSince = _awaitingResultSince.Value;

            if (resultTime is not null && resultTime.Value < awaitingSince - ResultTimeSlack)
            {
                _logger.LogInformation(
                    "打鍵カウンタ: プレイ開始（{Started:HH:mm:ss}）より前のリザルト（{ResultTime:HH:mm:ss}）のため無視します。",
                    awaitingSince, resultTime.Value);
                return;
            }

            _awaitingResultSince = null;
        }

        var fields = DakenCounterFieldMapper.MapResult(latest);
        _logger.LogInformation(
            "打鍵カウンタ: プレイリザルトを記録します（{Title} / {Diff}、EX スコア {ExScore}、{Lamp}）。",
            Describe(fields, FieldKeys.Title), Describe(fields, FieldKeys.DiffShort),
            Describe(fields, FieldKeys.ExScore), Describe(fields, FieldKeys.Lamp));
        PlayResultDetected?.Invoke(this, new PlayResultEventArgs(receivedAt, fields));
    }

    /// <summary><c>history_cursong</c>: 曲情報のキャッシュを更新する。</summary>
    private void ProcessCurrentSong(JsonElement data, DateTimeOffset receivedAt)
    {
        var fields = DakenCounterFieldMapper.MapCurrentSong(data);
        lock (_gate)
        {
            if (fields.Count > 0)
                _currentSong = fields;
            AddFeedLocked(receivedAt);
        }
    }

    private void AddFeed(DateTimeOffset receivedAt)
    {
        lock (_gate) AddFeedLocked(receivedAt);
    }

    private void AddFeedLocked(DateTimeOffset receivedAt)
    {
        _recentFeeds.Add(receivedAt);
        if (_recentFeeds.Count > MaxRecentFeeds)
            _recentFeeds.RemoveAt(0);
    }

    private DateTimeOffset? LastFeedLocked()
        => _recentFeeds.Count > 0 ? _recentFeeds[^1] : _connectedAt;

    /// <summary>再送の束（重複した today_updates の直前 <see cref="ResendBurstWindow"/> 以内の受信）を取り消す。</summary>
    private void RollBackResendLocked(DateTimeOffset resendAt)
    {
        var removed = 0;
        while (_recentFeeds.Count > 0 && resendAt - _recentFeeds[^1] <= ResendBurstWindow)
        {
            _recentFeeds.RemoveAt(_recentFeeds.Count - 1);
            removed++;
        }

        if (removed > 0)
            _logger.LogDebug("打鍵カウンタ: リザルトの再送のため、直前の受信 {Count} 件をプレイ開始の判定から外します。", removed);
    }

    /// <summary>リザルトの同一判定に使う内容（時刻 + 譜面 + 成績）。</summary>
    private static string BuildResultKey(JsonElement item)
    {
        var parts = new List<string>(5);
        foreach (var name in new[] { "timestamp", "chart_id", "score", "bp", "lamp" })
        {
            parts.Add(item.TryGetProperty(name, out var value)
                ? value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? string.Empty,
                    JsonValueKind.Number => value.GetRawText(),
                    _ => string.Empty,
                }
                : string.Empty);
        }
        return string.Join('/', parts);
    }

    /// <summary>
    /// <c>today_updates</c> の <c>items</c>（名前に反して本日分の全件）から最新のエントリを取り出す。
    /// 並び順に依存しないよう <c>timestamp</c> の最大値で選び、時刻を持つエントリが 1 件も無ければ先頭を採る。
    /// </summary>
    private static bool TryGetLatestResult(JsonElement data, out JsonElement latest, out DateTimeOffset? time)
    {
        latest = default;
        time = null;

        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var found = false;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (DakenCounterFieldMapper.TryGetResultTime(item, out var itemTime))
            {
                if (time is null || itemTime > time.Value)
                {
                    latest = item;
                    time = itemTime;
                }
            }
            else if (!found)
            {
                latest = item;
            }

            found = true;
        }

        return found;
    }

    private static string Describe(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : "-";

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
