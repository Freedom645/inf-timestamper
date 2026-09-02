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
/// <b>プレイ開始</b>は <c>nowplaying</c> のうち曲決定画面（楽曲情報画面）由来のものだけを採る。
/// 選曲画面でカーソルが動いたときにも同じ型のメッセージが飛ぶため、切り出し画像の有無で選り分ける
/// （判定は <see cref="SdvxFieldMapper.IsSongDecided"/>）。曲決定 1 回につき 1 通しか飛ばない。
/// </description></item>
/// <item><description>
/// <b>プレイリザルト</b>は <c>today_results</c>（リザルトを DB へ登録したときに飛ぶ）の最新エントリを採る。
/// 接続直後にもキャッシュ済みの本日分がまとめて飛んでくるので、プレイ開始を検知していない間は無視する。
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

    /// <summary>1 メッセージの上限。<c>nowplaying</c> は base64 の切り出し画像を含むため大きめに取る。</summary>
    private const int MaxMessageBytes = 16 * 1024 * 1024;

    private const int ReceiveBufferBytes = 64 * 1024;

    private readonly ILogger<SdvxHelperPlayWatcher> _logger;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private DateTimeOffset? _awaitingResultSince;
    private bool _disposed;

    public SdvxHelperPlayWatcher()
        : this(NullLogger<SdvxHelperPlayWatcher>.Instance) { }

    public SdvxHelperPlayWatcher(ILogger<SdvxHelperPlayWatcher> logger)
    {
        _logger = logger ?? NullLogger<SdvxHelperPlayWatcher>.Instance;
    }

    /// <summary>プレイ開始検知。CapturedAt は曲決定を受信した時刻。Fields は曲情報のみ。</summary>
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は today_results の最新エントリから展開した各識別子。</summary>
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public bool IsRunning
    {
        get { lock (_gate) return _cts is not null; }
    }

    /// <summary><paramref name="source"/> は <c>ws://host:port</c> 形式の接続先。</summary>
    public void Start(string source)
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
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            _loopTask = Task.Run(() => RunAsync(endpoint, token), token);

            _logger.LogInformation("SDVX Helper への接続を開始します: {Endpoint}", endpoint);
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
            _awaitingResultSince = null;
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

    /// <summary>曲決定画面由来の nowplaying だけをプレイ開始として扱う。</summary>
    private void ProcessNowPlaying(JsonElement data)
    {
        if (!SdvxFieldMapper.IsSongDecided(data))
        {
            _logger.LogDebug("SDVX: 選曲画面の nowplaying のため無視します。");
            return;
        }

        var startedAt = DateTimeOffset.Now;
        var fields = SdvxFieldMapper.MapNowPlaying(data);

        lock (_gate)
        {
            _awaitingResultSince = startedAt;
        }

        _logger.LogDebug("SDVX: プレイ開始を検知しました（{Count} フィールド）。", fields.Count);
        PlayStarted?.Invoke(this, new PlayStartedEventArgs(startedAt, fields));
    }

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
            _logger.LogDebug(
                "SDVX: プレイ開始 {Started} より前のリザルト（{ResultTime}）のため無視します。",
                awaitingSince, resultTime.Value);
            return;
        }

        lock (_gate)
        {
            _awaitingResultSince = null;
        }

        var fields = SdvxFieldMapper.MapResult(latest);
        _logger.LogDebug("SDVX: プレイリザルトを検知 ({Count} フィールド)", fields.Count);
        PlayResultDetected?.Invoke(this, new PlayResultEventArgs(DateTimeOffset.Now, fields));
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
