using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Sdvx;

/// <summary>
/// SDVX Helper のデータ配信 WebSocket（既定 <c>ws://127.0.0.1:8767</c>）を購読し、
/// プレイ開始 / プレイリザルトを検知する。
///
/// INFINITAS / pop'n music と違い、SDVX Helper は v2 系でファイル出力（<c>out/history_cursong.xml</c>）を
/// 廃止して WebSocket 配信へ移行しているため、ここだけ取得方式が違う。
///
/// 検知の要点:
/// <list type="bullet">
/// <item><description>
/// <b>プレイ開始</b>は SDVX Helper のログ（<see cref="SdvxHelperLogTail"/>）の「プレイ画面へ遷移」だけで決める。
/// WebSocket にはプレイ開始のイベントが無く、曲決定画面の <c>nowplaying</c> で代用すると
/// リザルト画面からのリトライ（選曲も曲決定も通らない）を落とすため。
/// SDVX Helper のフォルダが未設定でログを使えないときに限り、<c>nowplaying</c> で代用する。
/// </description></item>
/// <item><description>
/// <c>nowplaying</c> は<b>曲情報のキャッシュ</b>として扱う。曲決定画面由来のものでキャッシュを更新し、
/// 選曲画面由来のもの（＝別の曲を選び直している）でキャッシュを捨てる。プレイ開始時にその時点の
/// キャッシュを添えるので、リトライでも直前と同じ譜面の曲情報が入る。
/// 曲決定画面かどうかは切り出し画像の有無で判定する（<see cref="SdvxFieldMapper.IsSongDecided"/>）。
/// </description></item>
/// <item><description>
/// <b>プレイリザルト</b>は <c>today_results</c>（リザルトを DB へ登録したときに飛ぶ）の最新エントリを採る。
/// 接続直後にもキャッシュ済みの本日分がまとめて飛んでくるので、プレイ開始を検知していない間は無視する。
/// SDVX Helper が同じリザルトを 2 回登録することがあるため、同一エントリ（譜面 + 時刻）は 1 度しか採らない。
/// </description></item>
/// </list>
/// 接続失敗・切断は指数バックオフで再接続し、配信の記録自体には影響させない（ログのみ）。
/// </summary>
public sealed class SdvxHelperPlayWatcher : IPlayWatcher
{
    /// <summary>
    /// <c>today_results</c> のエントリ時刻がプレイ開始より前なら前のプレイのものとみなして捨てる。
    /// 時刻は秒精度なので、丸めで手前に出るぶんだけ猶予を持たせる。
    /// </summary>
    private static readonly TimeSpan ResultTimeSlack = TimeSpan.FromSeconds(2);

    /// <summary>
    /// SDVX Helper がリザルト画面を 2 回読み取り、同じプレイを 2 回登録することがある
    /// （実ログでは 1〜2 秒差。登録ごとに <c>timestamp</c> がずれるので時刻では同一判定できない）。
    /// この時間内に同じ内容のリザルトが来たら二重登録とみなす。
    /// 同じ譜面を同じスコアでリトライし切るには短すぎる長さにしてある。
    /// </summary>
    private static readonly TimeSpan DuplicateResultWindow = TimeSpan.FromSeconds(30);

    /// <summary>1 メッセージの上限。<c>nowplaying</c> は base64 の切り出し画像を含むため大きめに取る。</summary>
    private const int MaxMessageBytes = 16 * 1024 * 1024;

    private const int ReceiveBufferBytes = 64 * 1024;


    private readonly ILogger<SdvxHelperPlayWatcher> _logger;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private SdvxHelperLogTail? _logTail;
    private DateTimeOffset? _awaitingResultSince;

    /// <summary>直近に曲決定画面で見えた曲情報。プレイ開始時に添える。</summary>
    private IReadOnlyDictionary<string, string>? _pendingSongFields;

    /// <summary>採用済みリザルトの内容（譜面 + 成績）。SDVX Helper の二重登録を弾く。</summary>
    private string? _lastConsumedResultKey;

    /// <summary>そのリザルトを採用した時刻。<see cref="DuplicateResultWindow"/> の判定に使う。</summary>
    private DateTimeOffset _lastConsumedResultAt;

    private bool _disposed;

    public SdvxHelperPlayWatcher()
        : this(NullLogger<SdvxHelperPlayWatcher>.Instance) { }

    public SdvxHelperPlayWatcher(ILogger<SdvxHelperPlayWatcher> logger)
    {
        _logger = logger ?? NullLogger<SdvxHelperPlayWatcher>.Instance;
    }

    /// <summary>
    /// プレイ開始検知。CapturedAt はログのプレイ画面遷移時刻
    /// （ログ監視が無いときは曲決定を受信した時刻）。
    /// Fields は直近の曲決定画面で見えた曲情報。取れていなければ空（リザルトで埋まる）。
    /// </summary>
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は today_results の最新エントリから展開した各識別子。</summary>
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public bool IsRunning
    {
        get { lock (_gate) return _cts is not null; }
    }

    /// <summary>
    /// <see cref="WatchTarget.Endpoint"/> は <c>ws://host:port</c> 形式の接続先（必須）。
    /// <see cref="WatchTarget.Directory"/> は SDVX Helper のフォルダ（任意。ログ監視に使う）。
    /// </summary>
    public void Start(WatchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Start(target.Endpoint ?? string.Empty, target.Directory);
    }

    /// <summary><paramref name="source"/> は <c>ws://host:port</c> 形式の接続先。</summary>
    public void Start(string source, string? helperDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("SDVX Helper の接続先が指定されていません。", nameof(source));

        if (!Uri.TryCreate(source, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != "ws" && endpoint.Scheme != "wss"))
        {
            throw new ArgumentException($"SDVX Helper の接続先が不正です: {source}", nameof(source));
        }

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SdvxHelperPlayWatcher));
            if (_cts is not null)
                throw new InvalidOperationException("既に監視が動作中です。");

            _awaitingResultSince = null;
            _pendingSongFields = null;
            _lastConsumedResultKey = null;
            _lastConsumedResultAt = default;
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            _loopTask = Task.Run(() => RunAsync(endpoint, token), token);

            _logger.LogInformation("SDVX Helper への接続を開始します: {Endpoint}", endpoint);

            // ログ監視は任意。張れなくても WebSocket だけで動く（リトライは拾えなくなる）
            if (!string.IsNullOrWhiteSpace(helperDirectory))
            {
                var tail = new SdvxHelperLogTail(_logger);
                try
                {
                    tail.PlayEntered += OnLogPlayEntered;
                    tail.Start(helperDirectory);
                    _logTail = tail;
                }
                catch (Exception ex)
                {
                    tail.PlayEntered -= OnLogPlayEntered;
                    tail.Dispose();
                    _logger.LogWarning(ex,
                        "SDVX: ログ監視を開始できませんでした。曲決定画面の検知だけで動作します（リトライは記録されません）: {Directory}",
                        helperDirectory);
                }
            }
            else
            {
                _logger.LogInformation(
                    "SDVX: SDVX Helper のフォルダが未設定のため、プレイ開始は曲決定画面の検知だけで判定します（リトライは記録されません）。");
            }
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loopTask;

        SdvxHelperLogTail? tail;
        lock (_gate)
        {
            if (_cts is null) return;
            cts = _cts;
            loopTask = _loopTask;
            tail = _logTail;
            _cts = null;
            _loopTask = null;
            _logTail = null;
            _awaitingResultSince = null;
            _pendingSongFields = null;
            _lastConsumedResultKey = null;
            _lastConsumedResultAt = default;
        }

        if (tail is not null)
        {
            tail.PlayEntered -= OnLogPlayEntered;
            tail.Dispose();
        }

        try { cts.Cancel(); } catch (ObjectDisposedException) { /* ignore */ }

        if (loopTask is not null)
        {
            try { await loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* 想定内 */ }
            catch (Exception ex) { _logger.LogDebug(ex, "SDVX Helper の受信ループの終了時に例外が発生しました。"); }
        }

        cts.Dispose();
        _logger.LogInformation("SDVX Helper への接続を停止しました。");
    }

    /// <summary>接続 → 受信 → 切断されたらバックオフして再接続、を停止まで繰り返す。</summary>
    private async Task RunAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

                attempt = 0;
                _logger.LogInformation("SDVX Helper に接続しました: {Endpoint}", endpoint);

                await ReceiveLoopAsync(socket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                // 接続できないのは SDVX Helper が未起動なだけのことが多いので、警告は初回のみ
                if (attempt == 1)
                    _logger.LogWarning(ex, "SDVX Helper への接続が切れました。再接続します: {Endpoint}", endpoint);
                else
                    _logger.LogDebug(ex, "SDVX Helper への再接続に失敗しました（{Attempt} 回目）。", attempt);
            }

            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                await Task.Delay(BackoffSchedule.GetDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferBytes);
        try
        {
            var message = new ArrayBufferWriter<byte>(ReceiveBufferBytes);

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                message.Clear();
                bool oversized = false;

                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    // 上限を超えたぶんは捨てるが、フレームは最後まで読み切らないと次のメッセージとずれる
                    if (!oversized && message.WrittenCount + result.Count > MaxMessageBytes)
                    {
                        oversized = true;
                        _logger.LogWarning("SDVX Helper から上限を超えるメッセージを受信したため破棄します。");
                    }

                    if (!oversized)
                        message.Write(buffer.AsSpan(0, result.Count));
                }
                while (!result.EndOfMessage);

                if (oversized || result.MessageType != WebSocketMessageType.Text || message.WrittenCount == 0)
                    continue;

                HandleMessage(message.WrittenMemory);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>受信した 1 メッセージ（UTF-8 の JSON）を処理する。テストからも直接呼ぶ。</summary>
    internal void HandleMessage(ReadOnlyMemory<byte> utf8Json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "SDVX Helper からのメッセージを解析できませんでした。");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return;
            if (!root.TryGetProperty("data", out var data)) return;

            if (type.ValueEquals("nowplaying"))
                ProcessNowPlaying(data);
            else if (type.ValueEquals("today_results"))
                ProcessTodayResults(data);
        }
    }

    /// <summary>
    /// <c>nowplaying</c> は曲情報のキャッシュとして扱う。
    /// ログ監視が使えないときだけ、曲決定をプレイ開始の代用にする。
    /// </summary>
    private void ProcessNowPlaying(JsonElement data)
    {
        if (!SdvxFieldMapper.IsSongDecided(data))
        {
            // 選曲画面でカーソルが動いた。別の曲を選び直しているので、前の曲情報は捨てる
            lock (_gate)
            {
                if (_pendingSongFields is not null)
                    _logger.LogDebug("SDVX: 選曲画面に戻ったため、保持していた曲情報を破棄します。");
                _pendingSongFields = null;
            }
            return;
        }

        var fields = SdvxFieldMapper.MapNowPlaying(data);
        bool hasLogTail;
        lock (_gate)
        {
            _pendingSongFields = fields;
            hasLogTail = _logTail is not null;
        }

        _logger.LogInformation(
            "SDVX: 曲決定画面を検知しました（{Title} / {Diff} Lv.{Level}）。",
            Describe(fields, FieldKeys.Title), Describe(fields, FieldKeys.DiffShort), Describe(fields, FieldKeys.Level));

        // ログ監視があるときはプレイ画面への遷移で起こす（曲決定してもプレイせずに戻ることがあるため）
        if (!hasLogTail)
            RaisePlayStarted(DateTimeOffset.Now, fields, "曲決定画面");
    }

    /// <summary>SDVX Helper のログでプレイ画面への遷移を検知したとき（プレイ開始の主信号）。</summary>
    private void OnLogPlayEntered(object? sender, DateTimeOffset at)
    {
        IReadOnlyDictionary<string, string> fields;
        lock (_gate)
        {
            fields = _pendingSongFields ?? new Dictionary<string, string>();
        }

        RaisePlayStarted(at, fields, "ログのプレイ画面遷移");
    }

    /// <summary>テスト用: ログにプレイ画面遷移が書かれたのと同じ経路を通す。</summary>
    internal void SimulateLogPlayEntered(DateTimeOffset at) => OnLogPlayEntered(this, at);

    private void RaisePlayStarted(DateTimeOffset startedAt, IReadOnlyDictionary<string, string> fields, string source)
    {
        lock (_gate)
        {
            _awaitingResultSince = startedAt;
        }

        _logger.LogInformation(
            "SDVX: プレイ開始を記録します（{Source}、{At:HH:mm:ss}、曲: {Title}）。",
            source, startedAt, Describe(fields, FieldKeys.Title));
        PlayStarted?.Invoke(this, new PlayStartedEventArgs(startedAt, fields));
    }

    private static string Describe(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : "-";

    /// <summary>today_results の最新エントリを、プレイ開始後のものだけ採用する。</summary>
    private void ProcessTodayResults(JsonElement data)
    {
        DateTimeOffset awaitingSince;
        lock (_gate)
        {
            if (_awaitingResultSince is null)
            {
                // プレイ開始を検知していない状態での配信（接続直後の本日分など）は無視する
                _logger.LogDebug("SDVX: リザルト待ちでないため today_results を無視します。");
                return;
            }
            awaitingSince = _awaitingResultSince.Value;
        }

        if (!TryGetLatestResult(data, out var latest, out var resultTime)) return;

        // 時刻が読めた場合はプレイ開始より後のものだけを採用（前のプレイの取り違え防止）。
        // 読めない場合は判定材料が無いので採用する。
        if (resultTime is not null && resultTime.Value < awaitingSince - ResultTimeSlack)
        {
            _logger.LogInformation(
                "SDVX: プレイ開始（{Started:HH:mm:ss}）より前のリザルト（{ResultTime:HH:mm:ss}）のため無視します。",
                awaitingSince, resultTime.Value);
            return;
        }

        // SDVX Helper がリザルト画面を 2 回読み取って同じプレイを 2 回登録することがある
        var key = BuildResultKey(latest);
        var now = DateTimeOffset.Now;
        lock (_gate)
        {
            if (key is not null && key == _lastConsumedResultKey
                && now - _lastConsumedResultAt < DuplicateResultWindow)
            {
                _logger.LogInformation(
                    "SDVX: 直前に採用したものと同じリザルト（{Key}）のため、二重登録とみなして無視します。", key);
                return;
            }
            _awaitingResultSince = null;
            _lastConsumedResultKey = key;
            _lastConsumedResultAt = now;
        }

        var fields = SdvxFieldMapper.MapResult(latest);
        _logger.LogInformation(
            "SDVX: プレイリザルトを記録します（{Title} / {Diff}、スコア {Score}、{Lamp}）。",
            Describe(fields, FieldKeys.Title), Describe(fields, FieldKeys.DiffShort),
            Describe(fields, FieldKeys.Score), Describe(fields, FieldKeys.ClearLamp));
        PlayResultDetected?.Invoke(this, new PlayResultEventArgs(DateTimeOffset.Now, fields));
    }

    /// <summary>
    /// 採用済みリザルトの識別子（譜面 + 成績）。
    /// <c>timestamp</c> は二重登録のたびにずれるので使わない。
    /// </summary>
    private static string? BuildResultKey(JsonElement item)
    {
        var parts = new List<string>(4);
        foreach (var name in new[] { "chart_id", "score", "exscore", "lamp" })
        {
            if (!item.TryGetProperty(name, out var value)) continue;
            parts.Add(value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty,
            });
        }
        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    /// <summary>
    /// <c>today_results</c> の <c>items</c> から最新のエントリを取り出す。
    /// 並び順に依存しないよう <c>timestamp</c> の最大値で選び、
    /// 時刻を持つエントリが 1 件も無ければ末尾を採る。
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

            if (SdvxFieldMapper.TryGetResultTime(item, out var itemTime))
            {
                if (time is null || itemTime >= time.Value)
                {
                    latest = item;
                    time = itemTime;
                }
            }
            else if (time is null)
            {
                // 時刻不明同士は後勝ち。時刻を持つエントリが既にあればそちらを優先する
                latest = item;
            }

            found = true;
        }

        return found;
    }

    /// <summary>テスト用: WebSocket 接続を張らずにリザルト待ち状態を作る。</summary>
    internal void ConfigureForTest(DateTimeOffset? awaitingResultSince = null)
    {
        lock (_gate)
        {
            _awaitingResultSince = awaitingResultSince;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
