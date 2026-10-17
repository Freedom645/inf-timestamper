using System.Buffers;
using System.Net.WebSockets;
using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;

namespace InfTimestamper.Core.Net;

/// <summary>
/// 外部ツールのデータ配信 WebSocket を購読し続ける受信ループ。
/// 接続 → 受信 → 切断されたら指数バックオフ（<see cref="BackoffSchedule"/>）で再接続、を停止まで繰り返す。
///
/// SDVX Helper と打鍵カウンタは同じ作者・同じ配信サーバ実装（<c>{"type": ..., "data": ...}</c> の封筒、
/// 接続時に最新データを再送）なので、この部分を共有している。メッセージの解釈は呼び出し側が持つ。
/// </summary>
internal sealed class WebSocketSubscription
{
    private const int ReceiveBufferBytes = 64 * 1024;

    private readonly string _toolName;
    private readonly int _maxMessageBytes;
    private readonly ILogger _logger;
    private readonly Action _onConnected;
    private readonly Action<ReadOnlyMemory<byte>> _onMessage;

    /// <param name="toolName">ログに出す接続先ツール名。</param>
    /// <param name="maxMessageBytes">1 メッセージの上限。超えたものは読み捨てる。</param>
    /// <param name="onConnected">接続が確立するたびに（受信を始める前に）呼ばれる。</param>
    /// <param name="onMessage">テキストメッセージ 1 通（UTF-8）を受け切るたびに呼ばれる。</param>
    public WebSocketSubscription(
        string toolName,
        int maxMessageBytes,
        ILogger logger,
        Action onConnected,
        Action<ReadOnlyMemory<byte>> onMessage)
    {
        _toolName = toolName;
        _maxMessageBytes = maxMessageBytes;
        _logger = logger;
        _onConnected = onConnected;
        _onMessage = onMessage;
    }

    /// <summary><c>ws</c> / <c>wss</c> の絶対 URI として解釈できるか。</summary>
    public static bool TryParseEndpoint(string? source, out Uri endpoint)
    {
        if (!string.IsNullOrWhiteSpace(source)
            && Uri.TryCreate(source, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == "ws" || parsed.Scheme == "wss"))
        {
            endpoint = parsed;
            return true;
        }

        endpoint = null!;
        return false;
    }

    /// <summary>接続 → 受信 → 切断されたらバックオフして再接続、を停止まで繰り返す。</summary>
    public async Task RunAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);

                attempt = 0;
                _logger.LogInformation("{Tool} に接続しました: {Endpoint}", _toolName, endpoint);
                _onConnected();

                await ReceiveLoopAsync(socket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                // 接続できないのは相手のツールが未起動なだけのことが多いので、警告は初回のみ
                if (attempt == 1)
                    _logger.LogWarning(ex, "{Tool} への接続が切れました。再接続します: {Endpoint}", _toolName, endpoint);
                else
                    _logger.LogDebug(ex, "{Tool} への再接続に失敗しました（{Attempt} 回目）。", _toolName, attempt);
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
                    if (!oversized && message.WrittenCount + result.Count > _maxMessageBytes)
                    {
                        oversized = true;
                        _logger.LogWarning("{Tool} から上限を超えるメッセージを受信したため破棄します。", _toolName);
                    }

                    if (!oversized)
                        message.Write(buffer.AsSpan(0, result.Count));
                }
                while (!result.EndOfMessage);

                if (oversized || result.MessageType != WebSocketMessageType.Text || message.WrittenCount == 0)
                    continue;

                _onMessage(message.WrittenMemory);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
