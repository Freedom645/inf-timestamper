using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Coordination;

public sealed class RecordingCoordinator : IAsyncDisposable
{
    private readonly AppStateMachine _stateMachine;
    private readonly IReadOnlyDictionary<GameId, IPlayWatcher> _watchers;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<IObsConnection> _streamConnectionFactory;
    private readonly Func<IObsConnection, ObsConnectionManager> _managerFactory;
    private readonly ILogger<RecordingCoordinator> _logger;

    private IPlayWatcher? _activeWatcher;
    private IObsConnection? _streamConnection;
    private ObsConnectionManager? _streamManager;
    private CancellationTokenSource? _cts;
    private Task? _managerTask;
    private bool _disposed;

    private RecordingCoordinatorOptions _options = new();

    public RecordingCoordinator(
        AppStateMachine stateMachine,
        IReadOnlyDictionary<GameId, IPlayWatcher> watchers,
        IUiDispatcher dispatcher,
        Func<IObsConnection> streamConnectionFactory,
        Func<IObsConnection, ObsConnectionManager> managerFactory,
        ILogger<RecordingCoordinator>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _watchers = watchers ?? throw new ArgumentNullException(nameof(watchers));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _streamConnectionFactory = streamConnectionFactory ?? throw new ArgumentNullException(nameof(streamConnectionFactory));
        _managerFactory = managerFactory ?? throw new ArgumentNullException(nameof(managerFactory));
        _logger = logger ?? NullLogger<RecordingCoordinator>.Instance;

        _stateMachine.StateChanged += OnStateChanged;

        // 監視は「記録中」の間だけ動くが、購読は生存期間を通して張っておく
        // （停止中のウォッチャーは何も発火しないため、動作中のものだけが通知を上げる）
        foreach (var watcher in _watchers.Values)
        {
            watcher.PlayStarted += OnPlayStarted;
            watcher.PlayResultDetected += OnPlayResultDetected;
        }
    }

    public RecordingCoordinatorOptions Options => _options;

    public ObsConnectionManagerState CurrentObsState => _streamManager?.State ?? ObsConnectionManagerState.Idle;
    public int CurrentRetryAttempt => _streamManager?.RetryAttempt ?? 0;

    public event EventHandler<PlayStartedEventArgs>? PlayStarted;
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;
    public event EventHandler<RecordingObsStatusChangedEventArgs>? ObsStatusChanged;

    public void Configure(RecordingCoordinatorOptions options)
    {
        var previous = _options;
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // 設定ダイアログで接続先や監視先を直した場合、動作中のものを張り直す。
        // 「停止 → 開始」をユーザに強いないための追従。
        if (_streamManager is not null && !SameObsTarget(previous.StreamObs, _options.StreamObs))
        {
            _logger.LogInformation("OBS 接続情報が変更されたため、接続を張り直します。");
            StopConnection();
            EnsureConnectionStarted();
        }

        if (_activeWatcher is not null
            && (previous.Game != _options.Game
                || previous.WatchTarget != _options.WatchTarget))
        {
            _logger.LogInformation("プレイ監視の設定が変更されたため、監視を張り直します。");
            StopWatcher();
            EnsurePlayWatcherStarted();
        }
    }

    private static bool SameObsTarget(ObsConnectionOptions? a, ObsConnectionOptions? b)
    {
        if (a is null || b is null) return ReferenceEquals(a, b);
        return string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
               && a.Port == b.Port
               && string.Equals(a.Password, b.Password, StringComparison.Ordinal);
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

        // ゲームのプレイ検知（外部ツールの出力の監視）が必要な状態（Recording）
        if (newState == AppState.Recording)
        {
            EnsurePlayWatcherStarted();
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

    private void EnsurePlayWatcherStarted()
    {
        if (_activeWatcher is not null) return;

        var game = _options.Game;
        if (!_watchers.TryGetValue(game, out var watcher))
        {
            _logger.LogWarning("{Game} のプレイ監視機構が登録されていないため、監視を開始しません。",
                GameCatalog.DisplayName(game));
            return;
        }

        var target = _options.WatchTarget;
        if (target.IsEmpty)
        {
            _logger.LogWarning("{Tool} の監視対象が未指定のため、監視を開始しません。",
                GameCatalog.WatcherToolName(game));
            return;
        }

        try
        {
            watcher.Start(target);
            _activeWatcher = watcher;
            _logger.LogInformation("{Tool} のプレイ監視を開始しました: {Target}",
                GameCatalog.WatcherToolName(game), target);
        }
        catch (Exception ex)
        {
            // 監視開始失敗は記録自体には影響しない（要件: ダイアログを出さずログのみ）
            _logger.LogWarning(ex, "{Tool} のプレイ監視の開始に失敗しました: {Target}",
                GameCatalog.WatcherToolName(game), target);
        }
    }

    private void StopAll()
    {
        StopWatcher();
        StopConnection();
    }

    private void StopWatcher()
    {
        if (_activeWatcher is null) return;
        try { _activeWatcher.StopAsync().GetAwaiter().GetResult(); } catch { /* swallow */ }
        _activeWatcher = null;
    }

    private void StopConnection()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }

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

        // OBS の StreamStateChanged は「変化」しか通知しないため、接続した時点の実状態は
        // 自分で問い合わせないと分からない（配信中にアプリを起動した／再接続中に配信が終わった）
        if (e.State == ObsConnectionManagerState.Connected)
            _ = SyncStreamStateAsync(_streamConnection, _cts?.Token ?? CancellationToken.None);
    }

    /// <summary>接続直後に OBS の実際の配信状態を問い合わせ、アプリの状態と食い違っていれば合わせる。</summary>
    private async Task SyncStreamStateAsync(IObsConnection? connection, CancellationToken cancellationToken)
    {
        if (connection is null) return;

        bool isStreaming;
        try
        {
            isStreaming = await connection.IsStreamActiveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            // 取れなくてもイベント経由の検知は生きているので、記録は妨げない
            _logger.LogWarning(ex, "OBS の配信状態の取得に失敗しました。");
            return;
        }

        if (_disposed || cancellationToken.IsCancellationRequested) return;

        _dispatcher.Invoke(() =>
        {
            try
            {
                if (isStreaming && _stateMachine.State == AppState.WaitingForStream)
                {
                    _logger.LogInformation("OBS 接続時に配信中を検知。記録中に遷移します。");
                    _stateMachine.DetectStreamStart();
                }
                else if (!isStreaming && _stateMachine.State == AppState.Recording)
                {
                    _logger.LogInformation("OBS 接続時に配信が終了していることを検知。記録終了に遷移します。");
                    _stateMachine.DetectStreamEnd();
                }
            }
            catch (InvalidStateTransitionException ex)
            {
                _logger.LogDebug(ex, "配信状態の同期と AppStateMachine の状態が一致しないため遷移をスキップ。");
            }
        });
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
        foreach (var watcher in _watchers.Values)
        {
            watcher.PlayStarted -= OnPlayStarted;
            watcher.PlayResultDetected -= OnPlayResultDetected;
        }
        StopAll();
        return ValueTask.CompletedTask;
    }
}

public sealed class RecordingCoordinatorOptions
{
    public ObsConnectionOptions? StreamObs { get; set; }

    /// <summary>記録対象のゲーム。どの <c>IPlayWatcher</c> を動かすかを決める。</summary>
    public GameId Game { get; set; } = GameId.Infinitas;

    /// <summary>
    /// ゲーム検知の監視対象。INFINITAS（Reflux）と pop'n music（popn-lively-tracker）は
    /// 出力ディレクトリ、SOUND VOLTEX（SDVX Helper）は <c>ws://host:port</c> の接続先 + 任意でフォルダ。
    /// </summary>
    public WatchTarget WatchTarget { get; set; } = WatchTarget.Empty;
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
