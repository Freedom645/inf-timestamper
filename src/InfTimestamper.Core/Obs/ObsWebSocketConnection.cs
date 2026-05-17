using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace InfTimestamper.Core.Obs;

public sealed class ObsWebSocketConnection : IObsConnection
{
    private const string PngDataUrlPrefix = "data:image/png;base64,";

    private readonly OBSWebsocket _client = new();
    private readonly ILogger<ObsWebSocketConnection> _logger;
    private readonly object _gate = new();

    private TaskCompletionSource<bool>? _connectTcs;

    public ObsWebSocketConnection() : this(NullLogger<ObsWebSocketConnection>.Instance) { }

    public ObsWebSocketConnection(ILogger<ObsWebSocketConnection> logger)
    {
        _logger = logger;
        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.StreamStateChanged += OnClientStreamStateChanged;
    }

    public bool IsConnected => _client.IsConnected;

    public event EventHandler? Connected;
    public event EventHandler<ObsDisconnectedEventArgs>? Disconnected;
    public event EventHandler<ObsStreamStateChangedEventArgs>? StreamStateChanged;

    public async Task ConnectAsync(ObsConnectionOptions options, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (_client.IsConnected) return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _connectTcs = tcs;
        }

        _client.WSTimeout = timeout;

        try
        {
            _client.ConnectAsync(options.ToWebSocketUrl(), options.Password ?? string.Empty);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token)))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeDisconnect();
            throw;
        }
        catch (OperationCanceledException)
        {
            SafeDisconnect();
            throw new TimeoutException($"OBS 認証完了まで {timeout.TotalSeconds} 秒以内に応答がありませんでした。");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_connectTcs, tcs))
                    _connectTcs = null;
            }
        }
    }

    public Task DisconnectAsync()
    {
        SafeDisconnect();
        return Task.CompletedTask;
    }

    public Task<ObsScreenshot> GetScreenshotAsync(string sourceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("ソース名が指定されていません。", nameof(sourceName));

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var capturedAt = DateTimeOffset.Now;
                var dataUrl = _client.GetSourceScreenshot(sourceName, "png");
                var bytes = DecodePngDataUrl(dataUrl);
                return new ObsScreenshot(bytes, capturedAt);
            },
            cancellationToken);
    }

    public Task<bool> IsStreamActiveAsync(CancellationToken cancellationToken)
    {
        return Task.Run<bool>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                OutputStatus status = _client.GetStreamStatus();
                return status.IsActive;
            },
            cancellationToken);
    }

    public Task<ObsServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var version = _client.GetVersion();
                string scene = string.Empty;
                try { scene = _client.GetCurrentProgramScene() ?? string.Empty; }
                catch (Exception ex) { _logger.LogDebug(ex, "現在のシーン取得に失敗しました。"); }
                return new ObsServerInfo(version?.OBSStudioVersion ?? string.Empty, scene);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetInputNamesAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<string>>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputs = _client.GetInputList();
                var names = new List<string>(inputs?.Count ?? 0);
                if (inputs is not null)
                {
                    foreach (var input in inputs)
                    {
                        if (!string.IsNullOrEmpty(input.InputName))
                            names.Add(input.InputName);
                    }
                }
                return names;
            },
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Connected -= OnClientConnected;
        _client.Disconnected -= OnClientDisconnected;
        _client.StreamStateChanged -= OnClientStreamStateChanged;
        SafeDisconnect();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void SafeDisconnect()
    {
        try
        {
            if (_client.IsConnected) _client.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OBS Disconnect 中に例外が発生しました。");
        }
    }

    private void OnClientConnected(object? sender, EventArgs e)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_gate) { tcs = _connectTcs; }
        tcs?.TrySetResult(true);
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnClientDisconnected(object? sender, ObsDisconnectionInfo info)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_gate) { tcs = _connectTcs; }
        if (tcs is not null)
        {
            var reason = info?.DisconnectReason ?? "OBS から接続前に切断されました。";
            tcs.TrySetException(new InvalidOperationException(reason));
        }

        Disconnected?.Invoke(this, new ObsDisconnectedEventArgs(info?.DisconnectReason));
    }

    private void OnClientStreamStateChanged(object? sender, StreamStateChangedEventArgs e)
    {
        var state = MapState(e.OutputState.State);
        StreamStateChanged?.Invoke(this, new ObsStreamStateChangedEventArgs(state, e.OutputState.IsActive));
    }

    private static ObsStreamState MapState(OutputState raw) => raw switch
    {
        OutputState.OBS_WEBSOCKET_OUTPUT_STARTING => ObsStreamState.Starting,
        OutputState.OBS_WEBSOCKET_OUTPUT_STARTED => ObsStreamState.Started,
        OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING => ObsStreamState.Stopping,
        OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED => ObsStreamState.Stopped,
        _ => ObsStreamState.Unknown,
    };

    internal static byte[] DecodePngDataUrl(string dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl))
            throw new InvalidDataException("OBS から空のスクリーンショットが返却されました。");

        var base64 = dataUrl.StartsWith(PngDataUrlPrefix, StringComparison.OrdinalIgnoreCase)
            ? dataUrl[PngDataUrlPrefix.Length..]
            : ExtractBase64Payload(dataUrl);

        return Convert.FromBase64String(base64);
    }

    private static string ExtractBase64Payload(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        return comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
    }
}
