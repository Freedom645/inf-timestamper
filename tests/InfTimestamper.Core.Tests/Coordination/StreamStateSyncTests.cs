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

/// <summary>
/// OBS の <c>StreamStateChanged</c> は「変化」しか通知しないため、接続確立時に実状態を
/// 問い合わせて食い違いを直す必要がある（配信中にアプリを起動した／再接続中に配信が終わった）。
/// </summary>
public class StreamStateSyncTests
{
    private static readonly ObsConnectionOptions DefaultObs = new("127.0.0.1", 4455, "");

    private static RecordingCoordinator BuildCoordinator(
        FakeObsConnection connection,
        out AppStateMachine state,
        string watchDirectory = "")
        => BuildCoordinator(connection, out state, out _, watchDirectory);

    private static RecordingCoordinator BuildCoordinator(
        FakeObsConnection connection,
        out AppStateMachine state,
        out TestDelayProvider delays,
        string watchDirectory = "")
    {
        state = new AppStateMachine();
        var watcher = new RefluxPlayWatcher(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);
        var delayProvider = new TestDelayProvider();
        delays = delayProvider;

        var coordinator = new RecordingCoordinator(
            state,
            new Dictionary<GameId, IPlayWatcher> { [GameId.Infinitas] = watcher },
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => connection,
            managerFactory: c => new ObsConnectionManager(
                c, NullLogger<ObsConnectionManager>.Instance, delayProvider, TimeSpan.FromMilliseconds(50)));

        coordinator.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = DefaultObs,
            Game = GameId.Infinitas,
            WatchTarget = WatchTarget.ForDirectory(watchDirectory),
        });
        return coordinator;
    }

    [Fact]
    public async Task AlreadyStreamingWhenConnected_TransitionsToRecording()
    {
        var connection = new FakeObsConnection { StreamActiveHandler = () => Task.FromResult(true) };
        var coordinator = BuildCoordinator(connection, out var state);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => state.State == AppState.Recording, 1000);

            Assert.Equal(AppState.Recording, state.State);
        }
    }

    [Fact]
    public async Task NotStreamingWhenConnected_StaysWaiting()
    {
        var connection = new FakeObsConnection { StreamActiveHandler = () => Task.FromResult(false) };
        var coordinator = BuildCoordinator(connection, out var state);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);
            await Task.Delay(50);

            Assert.Equal(AppState.WaitingForStream, state.State);
        }
    }

    [Fact]
    public async Task StreamStatusFailure_DoesNotChangeState()
    {
        var connection = new FakeObsConnection
        {
            StreamActiveHandler = () => throw new InvalidOperationException("OBS 応答なし"),
        };
        var coordinator = BuildCoordinator(connection, out var state);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);
            await Task.Delay(50);

            Assert.Equal(AppState.WaitingForStream, state.State);
        }
    }

    [Fact]
    public async Task StreamEndedDuringDisconnect_TransitionsToRecordingEndedOnReconnect()
    {
        var active = true;
        var connection = new FakeObsConnection { StreamActiveHandler = () => Task.FromResult(active) };
        var coordinator = BuildCoordinator(connection, out var state, out var delays);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => state.State == AppState.Recording, 1000);

            // 切断中に配信が終わっていたケース
            active = false;
            connection.SimulateDisconnect("test");

            // 再接続のバックオフ待ちを解放する
            await WaitUntilAsync(() => delays.PendingCount > 0, 1000);
            delays.ReleaseNext();

            await WaitUntilAsync(() => state.State == AppState.RecordingEnded, 2000);
            Assert.Equal(AppState.RecordingEnded, state.State);
        }
    }

    [Fact]
    public async Task Configure_WithDifferentObsTarget_ReconnectsWhileWaiting()
    {
        var connection = new FakeObsConnection { StreamActiveHandler = () => Task.FromResult(false) };
        var coordinator = BuildCoordinator(connection, out var state);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => connection.ConnectAttempts >= 1, 1000);
            var before = connection.ConnectAttempts;

            coordinator.Configure(new RecordingCoordinatorOptions
            {
                StreamObs = new ObsConnectionOptions("192.168.1.111", 4455, ""),
                Game = GameId.Infinitas,
                WatchTarget = WatchTarget.Empty,
            });

            await WaitUntilAsync(() => connection.ConnectAttempts > before, 1000);
            Assert.True(connection.ConnectAttempts > before);
        }
    }

    [Fact]
    public async Task Configure_WithSameObsTarget_DoesNotReconnect()
    {
        var connection = new FakeObsConnection { StreamActiveHandler = () => Task.FromResult(false) };
        var coordinator = BuildCoordinator(connection, out var state);

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);
            var before = connection.ConnectAttempts;

            coordinator.Configure(new RecordingCoordinatorOptions
            {
                StreamObs = new ObsConnectionOptions("127.0.0.1", 4455, ""),
                Game = GameId.Infinitas,
                WatchTarget = WatchTarget.Empty,
            });

            await Task.Delay(50);
            Assert.Equal(before, connection.ConnectAttempts);
        }
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
