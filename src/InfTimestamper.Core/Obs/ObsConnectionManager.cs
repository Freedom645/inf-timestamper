using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Obs;

public sealed class ObsConnectionManager
{
    private readonly IObsConnection _connection;
    private readonly ILogger<ObsConnectionManager> _logger;
    private readonly IDelayProvider _delayProvider;
    private readonly TimeSpan _attemptTimeout;

    private int _retryAttempt;

    public ObsConnectionManager(IObsConnection connection)
        : this(connection, NullLogger<ObsConnectionManager>.Instance) { }

    public ObsConnectionManager(IObsConnection connection, ILogger<ObsConnectionManager> logger)
        : this(connection, logger, SystemDelayProvider.Instance, TimeSpan.FromSeconds(5)) { }

    public ObsConnectionManager(
        IObsConnection connection,
        ILogger<ObsConnectionManager> logger,
        IDelayProvider delayProvider,
        TimeSpan attemptTimeout)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger<ObsConnectionManager>.Instance;
        _delayProvider = delayProvider ?? SystemDelayProvider.Instance;
        _attemptTimeout = attemptTimeout;
        State = ObsConnectionManagerState.Idle;
    }

    public ObsConnectionManagerState State { get; private set; }
    public int RetryAttempt => _retryAttempt;

    public event EventHandler<ObsConnectionManagerStateChangedEventArgs>? StateChanged;

    public async Task RunAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        try
        {
            await RunLoopAsync(options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SetState(ObsConnectionManagerState.Stopped, 0);
        }
    }

    private async Task RunLoopAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
    {
        _retryAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var isInitial = _retryAttempt == 0;
            SetState(isInitial ? ObsConnectionManagerState.Connecting : ObsConnectionManagerState.Reconnecting, _retryAttempt);

            bool connected;
            try
            {
                await _connection.ConnectAsync(options, _attemptTimeout, cancellationToken).ConfigureAwait(false);
                connected = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                connected = false;
                _logger.LogWarning(ex, "OBS 接続試行 {Attempt} 回目が失敗しました。", _retryAttempt + 1);
            }

            if (connected)
            {
                _retryAttempt = 0;
                SetState(ObsConnectionManagerState.Connected, 0);

                try
                {
                    await WaitForDisconnectAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation("OBS から切断を検知しました。再接続を開始します。");
            }

            if (cancellationToken.IsCancellationRequested) return;

            _retryAttempt++;
            var delay = BackoffSchedule.GetDelay(_retryAttempt);
            SetState(ObsConnectionManagerState.Reconnecting, _retryAttempt);

            try
            {
                await _delayProvider.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task WaitForDisconnectAsync(CancellationToken cancellationToken)
    {
        if (!_connection.IsConnected) return;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, ObsDisconnectedEventArgs e) => tcs.TrySetResult(true);

        _connection.Disconnected += Handler;
        try
        {
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                if (!_connection.IsConnected) return;
                await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _connection.Disconnected -= Handler;
        }
    }

    private void SetState(ObsConnectionManagerState state, int retryAttempt)
    {
        if (State == state && _retryAttempt == retryAttempt) return;

        State = state;
        _retryAttempt = retryAttempt;
        StateChanged?.Invoke(this, new ObsConnectionManagerStateChangedEventArgs(state, retryAttempt));
    }
}
