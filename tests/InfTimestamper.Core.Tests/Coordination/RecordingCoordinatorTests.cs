using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Reflux;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Obs;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Coordination;

public class RecordingCoordinatorTests
{
    private static readonly ObsConnectionOptions DefaultObs = new("127.0.0.1", 4455, "");

    private static RecordingCoordinator BuildCoordinator(
        out AppStateMachine state,
        out RefluxPlayWatcher watcher,
        out FakeObsConnection sharedConnection,
        string refluxDirectory = "")
    {
        state = new AppStateMachine();
        watcher = new RefluxPlayWatcher(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);
        var conn = new FakeObsConnection();
        sharedConnection = conn;
        var dispatcher = ImmediateUiDispatcher.Instance;

        var coordinator = new RecordingCoordinator(
            state,
            new Dictionary<GameId, IPlayWatcher> { [GameId.Infinitas] = watcher },
            dispatcher,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(c, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)));

        coordinator.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = DefaultObs,
            Game = GameId.Infinitas,
            WatchDirectory = refluxDirectory,
        });
        return coordinator;
    }

    [Fact]
    public async Task Start_TransitionsToWaitingForStream_LeavesObsManagerRunning()
    {
        var coordinator = BuildCoordinator(out var state, out _, out _);
        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState != ObsConnectionManagerState.Idle, 1000);

            Assert.NotEqual(ObsConnectionManagerState.Idle, coordinator.CurrentObsState);
        }
    }

    [Fact]
    public async Task ForceStart_AfterConnection_StartsRefluxWatcher()
    {
        using var dir = new TempDirectory();
        var coordinator = BuildCoordinator(out var state, out var watcher, out _, dir.Path);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);

            state.ForceStart();
            await WaitUntilAsync(() => watcher.IsRunning, 1000);

            Assert.True(watcher.IsRunning);
        }
    }

    [Fact]
    public async Task Stop_TransitionsToRecordingEnded_DisconnectsObs()
    {
        var coordinator = BuildCoordinator(out var state, out _, out var conn);
        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);

            state.ForceStart();
            state.Stop();

            await WaitUntilAsync(() => conn.DisposeCount > 0, 1000);
        }
    }

    [Fact]
    public async Task StopFromWaitingForStream_ReturnsToInitial_DisconnectsObs()
    {
        // 「配信開始待ち」で停止したときも OBS 連携を解除する（要件: 初期状態に戻す）
        var coordinator = BuildCoordinator(out var state, out _, out var conn);
        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);

            state.Stop();

            Assert.Equal(AppState.Initial, state.State);
            await WaitUntilAsync(() => conn.DisposeCount > 0, 1000);
        }
    }

    [Fact]
    public void OnPlayStartedFromWatcher_BubblesViaCoordinator()
    {
        var coordinator = BuildCoordinator(out _, out var watcher, out _);
        PlayStartedEventArgs? captured = null;
        coordinator.PlayStarted += (_, e) => captured = e;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("Sample", 11);

        Assert.NotNull(captured);
        Assert.Equal("Sample", captured!.Fields["title"]);
    }

    [Fact]
    public void OnPlayResultDetectedFromWatcher_BubblesViaCoordinator()
    {
        var coordinator = BuildCoordinator(out _, out var watcher, out _);
        PlayResultEventArgs? captured = null;
        coordinator.PlayResultDetected += (_, e) => captured = e;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("Sample", 11);
        harness.LeavePlay(new RefluxLatestJson { Bad = "1", Poor = "2" });

        Assert.NotNull(captured);
        Assert.Equal("3", captured!.Fields["miss_count"]);
    }

    [Fact]
    public void Configure_WithoutStreamObs_DoesNotStartConnection()
    {
        var state = new AppStateMachine();
        var watcher = new RefluxPlayWatcher(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);
        var conn = new FakeObsConnection();

        var coordinator = new RecordingCoordinator(
            state,
            new Dictionary<GameId, IPlayWatcher> { [GameId.Infinitas] = watcher },
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(c, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)));

        // StreamObs を設定しないまま Start
        state.Start();
        Assert.Equal(ObsConnectionManagerState.Idle, coordinator.CurrentObsState);
        Assert.Equal(0, conn.ConnectAttempts);
    }

    private static async Task WaitUntilAsync(Func<bool> cond, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (cond()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("条件が成立しませんでした。");
    }
}
