using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Obs;

public class ObsConnectionManagerTests
{
    private static readonly ObsConnectionOptions Options = new("127.0.0.1", 4455, "secret");
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task RunAsync_ConnectsSuccessfully_TransitionsToConnected()
    {
        var conn = new FakeObsConnection();
        using var cts = new CancellationTokenSource();
        var manager = new ObsConnectionManager(conn, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), AttemptTimeout);

        var states = new List<ObsConnectionManagerState>();
        manager.StateChanged += (_, e) => states.Add(e.State);

        var run = manager.RunAsync(Options, cts.Token);

        await WaitUntilAsync(() => manager.State == ObsConnectionManagerState.Connected);

        Assert.Equal(0, manager.RetryAttempt);
        Assert.Equal(1, conn.ConnectAttempts);
        Assert.Contains(ObsConnectionManagerState.Connecting, states);
        Assert.Contains(ObsConnectionManagerState.Connected, states);

        cts.Cancel();
        await run;
        Assert.Equal(ObsConnectionManagerState.Stopped, manager.State);
    }

    [Fact]
    public async Task RunAsync_OnDisconnect_ReconnectsUsingBackoff()
    {
        var conn = new FakeObsConnection();
        var delay = new TestDelayProvider();
        using var cts = new CancellationTokenSource();
        var manager = new ObsConnectionManager(conn, NullLogger<ObsConnectionManager>.Instance, delay, AttemptTimeout);

        var run = manager.RunAsync(Options, cts.Token);
        await WaitUntilAsync(() => manager.State == ObsConnectionManagerState.Connected);

        conn.SimulateDisconnect("test");

        // 1st backoff delay (1s)
        await WaitUntilAsync(() => delay.RequestedDelays.Count >= 1);
        Assert.Equal(TimeSpan.FromSeconds(1), delay.RequestedDelays[0]);
        Assert.Equal(ObsConnectionManagerState.Reconnecting, manager.State);
        Assert.Equal(1, manager.RetryAttempt);

        delay.ReleaseNext(); // 再接続試行へ
        await WaitUntilAsync(() => manager.State == ObsConnectionManagerState.Connected);
        Assert.Equal(2, conn.ConnectAttempts);
        Assert.Equal(0, manager.RetryAttempt); // 復帰でリセット

        cts.Cancel();
        await run;
    }

    [Fact]
    public async Task RunAsync_RepeatedFailures_FollowBackoffSchedule()
    {
        var conn = new FakeObsConnection
        {
            ConnectHandler = _ => Task.FromException(new InvalidOperationException("接続不可")),
        };
        var delay = new TestDelayProvider();
        using var cts = new CancellationTokenSource();
        var manager = new ObsConnectionManager(conn, NullLogger<ObsConnectionManager>.Instance, delay, AttemptTimeout);

        var run = manager.RunAsync(Options, cts.Token);

        // 1 → 5 → 10 → 30 → 60 のスケジュールを 5 段階確認
        var expected = new[] { 1, 5, 10, 30, 60 };
        for (int i = 0; i < expected.Length; i++)
        {
            await WaitUntilAsync(() => delay.RequestedDelays.Count >= i + 1);
            Assert.Equal(TimeSpan.FromSeconds(expected[i]), delay.RequestedDelays[i]);
            delay.ReleaseNext();
        }

        cts.Cancel();
        await run;
        Assert.Equal(ObsConnectionManagerState.Stopped, manager.State);
    }

    [Fact]
    public async Task RunAsync_Cancellation_TransitionsToStopped()
    {
        var conn = new FakeObsConnection();
        var delay = new TestDelayProvider();
        using var cts = new CancellationTokenSource();
        var manager = new ObsConnectionManager(conn, NullLogger<ObsConnectionManager>.Instance, delay, AttemptTimeout);

        var run = manager.RunAsync(Options, cts.Token);
        await WaitUntilAsync(() => manager.State == ObsConnectionManagerState.Connected);

        cts.Cancel();
        await run;
        Assert.Equal(ObsConnectionManagerState.Stopped, manager.State);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(5);
        }
        throw new TimeoutException("条件が制限時間内に成立しませんでした。");
    }
}
