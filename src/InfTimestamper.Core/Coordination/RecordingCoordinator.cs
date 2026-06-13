using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.Reflux;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Coordination;

public sealed class RecordingCoordinator : IAsyncDisposable
{
    private readonly AppStateMachine _stateMachine;
    private readonly RefluxPlayWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<IObsConnection> _streamConnectionFactory;
    private readonly Func<IObsConnection, ObsConnectionManager> _managerFactory;
    private readonly ILogger<RecordingCoordinator> _logger;

    private IObsConnection? _streamConnection;
    private ObsConnectionManager? _streamManager;
    private CancellationTokenSource? _cts;
    private Task? _managerTask;
    private bool _disposed;

    private RecordingCoordinatorOptions _options = new();

    public RecordingCoordinator(
        AppStateMachine stateMachine,
        RefluxPlayWatcher watcher,
        IUiDispatcher dispatcher,
        Func<IObsConnection> streamConnectionFactory,
        Func<IObsConnection, ObsConnectionManager> managerFactory,
        ILogger<RecordingCoordinator>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _streamConnectionFactory = streamConnectionFactory ?? throw new ArgumentNullException(nameof(streamConnectionFactory));
        _managerFactory = managerFactory ?? throw new ArgumentNullException(nameof(managerFactory));
        _logger = logger ?? NullLogger<RecordingCoordinator>.Instance;

        _stateMachine.StateChanged += OnStateChanged;
        _watcher.PlayStarted += OnPlayStarted;
        _watcher.PlayResultDetected += OnPlayResultDetected;
    }

    public RecordingCoordinatorOptions Options => _options;

    public ObsConnectionManagerState CurrentObsState => _streamManager?.State ?? ObsConnectionManagerState.Idle;
    public int CurrentRetryAttempt => _streamManager?.RetryAttempt ?? 0;

    public event EventHandler<PlayStartedEventArgs>? PlayStarted;
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;
    public event EventHandler<RecordingObsStatusChangedEventArgs>? ObsStatusChanged;

    public void Configure(RecordingCoordinatorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        if (_disposed) return;

        var newState = e.NewState;

        // 停止系: 全停止
        if (newState is AppState.Initial or AppState.RecordingEnded)
        {
            StopAll();
            return;
        }

        // 配信検知のため OBS 接続が必要な状態（WaitingForStream / Recording）
        if (newState is AppState.WaitingForStream or AppState.Recording)
        {
            EnsureConnectionStarted();
        }

        // ゲームのプレイ検知（Reflux ファイル監視）が必要な状態（Recording）
        if (newState == AppState.Recording)
        {
            EnsureRefluxWatcherStarted();
        }
    }

    private void EnsureConnectionStarted()
    {
        if (_streamManager is not null) return;

        if (_options.StreamObs is null)
        {
            _logger.LogWarning("OBS 接続設定が未指定のため、接続を開始しません。");
            return;
        }

        _cts = new CancellationTokenSource();
        _streamConnection = _streamConnectionFactory();
        _streamConnection.StreamStateChanged += OnObsStreamStateChanged;
        _streamManager = _managerFactory(_streamConnection);
        _streamManager.StateChanged += OnManagerStateChanged;

        _logger.LogInformation("OBS 接続を開始します: {Host}:{Port}", _options.StreamObs.Host, _options.StreamObs.Port);
        _managerTask = _streamManager.RunAsync(_options.StreamObs, _cts.Token);
    }

    private void OnObsStreamStateChanged(object? sender, ObsStreamStateChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            try
            {
                if (e.State == ObsStreamState.Started && _stateMachine.State == AppState.WaitingForStream)
                {
                    _logger.LogInformation("OBS の配信開始を検知。記録中に遷移します。");
                    _stateMachine.DetectStreamStart();
                }
                else if (e.State == ObsStreamState.Stopped && _stateMachine.State == AppState.Recording)
                {
                    _logger.LogInformation("OBS の配信終了を検知。記録終了に遷移します。");
                    _stateMachine.DetectStreamEnd();
                }
            }
            catch (InvalidStateTransitionException ex)
            {
                _logger.LogDebug(ex, "OBS の配信状態変化と AppStateMachine の状態が一致しないため遷移をスキップ。");
            }
        });
    }

    private void EnsureRefluxWatcherStarted()
    {
        if (_watcher.IsRunning) return;

        if (string.IsNullOrWhiteSpace(_options.RefluxDirectory))
        {
            _logger.LogWarning("Reflux 出力ディレクトリが未指定のため、ファイル監視を開始しません。");
            return;
        }

        try
        {
            _watcher.Start(_options.RefluxDirectory);
            _logger.LogInformation("Reflux ファイル監視を開始しました: {Directory}", _options.RefluxDirectory);
        }
        catch (Exception ex)
        {
            // 監視開始失敗は記録自体には影響しない（要件: ダイアログを出さずログのみ）
            _logger.LogWarning(ex, "Reflux ファイル監視の開始に失敗しました: {Directory}", _options.RefluxDirectory);
        }
    }

    private void StopAll()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }

        try { _watcher.StopAsync().GetAwaiter().GetResult(); } catch { /* swallow */ }

        if (_streamManager is not null)
        {
            _streamManager.StateChanged -= OnManagerStateChanged;
            _streamManager = null;
        }

        if (_streamConnection is not null)
        {
            _streamConnection.StreamStateChanged -= OnObsStreamStateChanged;
            try { _streamConnection.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { /* swallow */ }
            _streamConnection = null;
        }

        _cts?.Dispose();
        _cts = null;
        _managerTask = null;
    }

    private void OnManagerStateChanged(object? sender, ObsConnectionManagerStateChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
            ObsStatusChanged?.Invoke(this, new RecordingObsStatusChangedEventArgs(e.State, e.RetryAttempt)));
    }

    private void OnPlayStarted(object? sender, PlayStartedEventArgs e)
        => _dispatcher.Invoke(() => PlayStarted?.Invoke(this, e));

    private void OnPlayResultDetected(object? sender, PlayResultEventArgs e)
        => _dispatcher.Invoke(() => PlayResultDetected?.Invoke(this, e));

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _stateMachine.StateChanged -= OnStateChanged;
        _watcher.PlayStarted -= OnPlayStarted;
        _watcher.PlayResultDetected -= OnPlayResultDetected;
        StopAll();
        return ValueTask.CompletedTask;
    }
}

public sealed class RecordingCoordinatorOptions
{
    public ObsConnectionOptions? StreamObs { get; set; }
    public string RefluxDirectory { get; set; } = string.Empty;
}

public sealed class RecordingObsStatusChangedEventArgs : EventArgs
{
    public RecordingObsStatusChangedEventArgs(ObsConnectionManagerState state, int retryAttempt)
    {
        State = state;
        RetryAttempt = retryAttempt;
    }

    public ObsConnectionManagerState State { get; }
    public int RetryAttempt { get; }
}
