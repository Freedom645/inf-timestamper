using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Obs;

public sealed class ObsScreenshotCapture : IAsyncDisposable
{
    public const int FailureLogThreshold = 5;

    private readonly IObsConnection _connection;
    private readonly ILogger<ObsScreenshotCapture> _logger;
    private readonly TimeSpan _period;
    private readonly object _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public ObsScreenshotCapture(IObsConnection connection)
        : this(connection, NullLogger<ObsScreenshotCapture>.Instance, TimeSpan.FromSeconds(1)) { }

    public ObsScreenshotCapture(
        IObsConnection connection,
        ILogger<ObsScreenshotCapture> logger,
        TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period), period, "取得周期は正の値を指定してください。");

        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger<ObsScreenshotCapture>.Instance;
        _period = period;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate) return _loopTask is not null && !_loopTask.IsCompleted;
        }
    }

    public int ConsecutiveFailureCount { get; private set; }

    public event EventHandler<ObsScreenshotCapturedEventArgs>? ScreenshotCaptured;
    public event EventHandler<ObsScreenshotFailureEventArgs>? CaptureFailed;

    public void Start(string sourceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("ソース名が指定されていません。", nameof(sourceName));

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ObsScreenshotCapture));
            if (_loopTask is not null && !_loopTask.IsCompleted)
                throw new InvalidOperationException("既に取得ループが動作中です。");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsecutiveFailureCount = 0;
            _loopTask = Task.Run(() => RunLoopAsync(sourceName, _cts.Token));
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loopTask;
        }

        if (cts is null || loop is null) return;

        try { cts.Cancel(); } catch { /* ignore */ }
        try { await loop.ConfigureAwait(false); } catch { /* swallow loop exceptions */ }

        lock (_gate)
        {
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    private async Task RunLoopAsync(string sourceName, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_period);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await CaptureOnceAsync(sourceName, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* 正常停止 */ }
    }

    private async Task CaptureOnceAsync(string sourceName, CancellationToken cancellationToken)
    {
        try
        {
            var screenshot = await _connection.GetScreenshotAsync(sourceName, cancellationToken).ConfigureAwait(false);
            OnSuccess(screenshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
        }
    }

    private void OnSuccess(ObsScreenshot screenshot)
    {
        var previousFailures = ConsecutiveFailureCount;
        ConsecutiveFailureCount = 0;

        if (previousFailures >= FailureLogThreshold)
            _logger.LogInformation("画面取得が復帰しました（直前まで {Failures} 回連続失敗）。", previousFailures);

        ScreenshotCaptured?.Invoke(this, new ObsScreenshotCapturedEventArgs(screenshot));
    }

    private void OnFailure(Exception ex)
    {
        ConsecutiveFailureCount++;

        if (ConsecutiveFailureCount == FailureLogThreshold)
            _logger.LogWarning(ex, "画面取得が {Threshold} 回連続で失敗しました。", FailureLogThreshold);

        CaptureFailed?.Invoke(this, new ObsScreenshotFailureEventArgs(ex, ConsecutiveFailureCount));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}

public sealed class ObsScreenshotCapturedEventArgs : EventArgs
{
    public ObsScreenshotCapturedEventArgs(ObsScreenshot screenshot)
    {
        Screenshot = screenshot;
    }

    public ObsScreenshot Screenshot { get; }
}

public sealed class ObsScreenshotFailureEventArgs : EventArgs
{
    public ObsScreenshotFailureEventArgs(Exception exception, int consecutiveFailureCount)
    {
        Exception = exception;
        ConsecutiveFailureCount = consecutiveFailureCount;
    }

    public Exception Exception { get; }
    public int ConsecutiveFailureCount { get; }
}
