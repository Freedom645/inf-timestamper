using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Coordination;

public sealed class RecordingCoordinator : IAsyncDisposable
{
    private readonly AppStateMachine _stateMachine;
    private readonly RecognitionPipeline _pipeline;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<IObsConnection> _streamConnectionFactory;
    private readonly Func<IObsConnection>? _captureConnectionFactory;
    private readonly Func<IObsConnection, ObsConnectionManager> _managerFactory;
    private readonly Func<IObsConnection, ObsScreenshotCapture> _captureFactory;
    private readonly ILogger<RecordingCoordinator> _logger;

    private IObsConnection? _streamConnection;
    private IObsConnection? _captureConnection;
    private ObsConnectionManager? _streamManager;
    private ObsScreenshotCapture? _screenshotCapture;
    private CancellationTokenSource? _cts;
    private Task? _managerTask;
    private bool _disposed;

    private RecordingCoordinatorOptions _options = new();

    public RecordingCoordinator(
        AppStateMachine stateMachine,
        RecognitionPipeline pipeline,
        IUiDispatcher dispatcher,
        Func<IObsConnection> streamConnectionFactory,
        Func<IObsConnection, ObsConnectionManager> managerFactory,
        Func<IObsConnection, ObsScreenshotCapture> captureFactory,
        Func<IObsConnection>? captureConnectionFactory = null,
        ILogger<RecordingCoordinator>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _streamConnectionFactory = streamConnectionFactory ?? throw new ArgumentNullException(nameof(streamConnectionFactory));
        _managerFactory = managerFactory ?? throw new ArgumentNullException(nameof(managerFactory));
        _captureFactory = captureFactory ?? throw new ArgumentNullException(nameof(captureFactory));
        _captureConnectionFactory = captureConnectionFactory;
        _logger = logger ?? NullLogger<RecordingCoordinator>.Instance;

        _stateMachine.StateChanged += OnStateChanged;
        _pipeline.PlayStarted += OnPlayStarted;
        _pipeline.PlayResultDetected += OnPlayResultDetected;
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

        // 接続が必要な状態（WaitingForStream / Recording）
        if (newState is AppState.WaitingForStream or AppState.Recording)
        {
            EnsureConnectionStarted();
        }

        // 画面取得が必要な状態（Recording）
        if (newState == AppState.Recording)
        {
            EnsureCaptureStarted();
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
        _streamManager = _managerFactory(_streamConnection);
        _streamManager.StateChanged += OnManagerStateChanged;

        _logger.LogInformation("OBS 接続を開始します: {Host}:{Port}", _options.StreamObs.Host, _options.StreamObs.Port);
        _managerTask = _streamManager.RunAsync(_options.StreamObs, _cts.Token);
    }

    private void EnsureCaptureStarted()
    {
        if (_screenshotCapture is not null) return;

        if (string.IsNullOrEmpty(_options.GameSourceName))
        {
            _logger.LogWarning("OBS ゲーム画面ソース名が未指定のため、画面取得を開始しません。");
            return;
        }

        var captureConn = ResolveCaptureConnection();
        if (captureConn is null) return;

        _screenshotCapture = _captureFactory(captureConn);
        _screenshotCapture.ScreenshotCaptured += OnScreenshotCaptured;
        _screenshotCapture.CaptureFailed += OnScreenshotFailed;

        _logger.LogInformation("画面取得ループを開始します: ソース={Source}", _options.GameSourceName);
        _screenshotCapture.Start(_options.GameSourceName, _cts?.Token ?? CancellationToken.None);
    }

    private IObsConnection? ResolveCaptureConnection()
    {
        // 1 台 PC 構成または Two PC 無効: Stream 接続を共用
        if (!_options.TwoPcEnabled || _captureConnectionFactory is null)
            return _streamConnection;

        // 2 台 PC 構成: 別接続を作成（D2a では Stream 接続のみ管理する簡易構成）
        // 別接続のライフサイクル管理は D2b 以降で本格化
        if (_captureConnection is null)
        {
            _captureConnection = _captureConnectionFactory();
            _logger.LogInformation("2 台 PC 構成: ゲーム画面取得用 OBS への接続を確立予定（D2b で結線）");
        }
        return _captureConnection;
    }

    private void StopAll()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }

        if (_screenshotCapture is not null)
        {
            _screenshotCapture.ScreenshotCaptured -= OnScreenshotCaptured;
            _screenshotCapture.CaptureFailed -= OnScreenshotFailed;
            try { _screenshotCapture.StopAsync().GetAwaiter().GetResult(); } catch { /* swallow */ }
            _screenshotCapture = null;
        }

        if (_streamManager is not null)
        {
            _streamManager.StateChanged -= OnManagerStateChanged;
            _streamManager = null;
        }

        if (_streamConnection is not null)
        {
            try { _streamConnection.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { /* swallow */ }
            _streamConnection = null;
        }

        if (_captureConnection is not null)
        {
            try { _captureConnection.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { /* swallow */ }
            _captureConnection = null;
        }

        _cts?.Dispose();
        _cts = null;
        _managerTask = null;
    }

    private void OnScreenshotCaptured(object? sender, ObsScreenshotCapturedEventArgs e)
    {
        try
        {
            _pipeline.ProcessFrame(e.Screenshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "認識パイプラインでエラーが発生しました。");
        }
    }

    private void OnScreenshotFailed(object? sender, ObsScreenshotFailureEventArgs e)
    {
        // 認識失敗自体は ObsScreenshotCapture 側でログ済み。ここでは UI 通知に振り替え可能だが
        // 現状は静観（要件: ダイアログを出さない、ログのみ）
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
        _pipeline.PlayStarted -= OnPlayStarted;
        _pipeline.PlayResultDetected -= OnPlayResultDetected;
        StopAll();
        return ValueTask.CompletedTask;
    }
}

public sealed class RecordingCoordinatorOptions
{
    public ObsConnectionOptions? StreamObs { get; set; }
    public ObsConnectionOptions? CaptureObs { get; set; }
    public bool TwoPcEnabled { get; set; }
    public string GameSourceName { get; set; } = string.Empty;
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
