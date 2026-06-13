using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.Reflux;
using InfTimestamper.Core.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Reflux;

public class RefluxPlayWatcherTests
{
    private static RefluxPlayWatcher NewWatcher()
        => new(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);

    [Fact]
    public void EnterPlay_RaisesPlayStartedWithTitleAndLevel()
    {
        var watcher = NewWatcher();
        PlayStartedEventArgs? started = null;
        watcher.PlayStarted += (_, e) => started = e;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("AA", 12);

        Assert.NotNull(started);
        Assert.Equal("AA", started!.Fields[RecognitionFieldKeys.Title]);
        Assert.Equal("12", started.Fields[RecognitionFieldKeys.Level]);
    }

    [Fact]
    public void LeavePlay_RaisesPlayResultFromLatestJson()
    {
        var watcher = NewWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("AA", 12);
        harness.LeavePlay(new RefluxLatestJson { Grade = "AA", Lamp = "HC", ExScore = "1000" });

        Assert.NotNull(result);
        Assert.Equal("AA", result!.Fields[RecognitionFieldKeys.DjLevel]);
        Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        Assert.Equal("1000", result.Fields[RecognitionFieldKeys.ExScore]);
    }

    [Fact]
    public void SameStatus_DoesNotRaiseEvents()
    {
        var watcher = NewWatcher();
        var startedCount = 0;
        watcher.PlayStarted += (_, _) => startedCount++;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("AA", 12);
        // 同じ play 状態を再通知しても多重発火しない
        harness.SetPlayState(RefluxPlayWatcher.PlayStatePlay);

        Assert.Equal(1, startedCount);
    }

    [Fact]
    public void PlayToOff_AlsoRaisesResult()
    {
        var watcher = NewWatcher();
        var resultCount = 0;
        watcher.PlayResultDetected += (_, _) => resultCount++;

        using var harness = new RefluxTestHarness(watcher);
        harness.EnterPlay("AA", 12);
        harness.LeavePlay(new RefluxLatestJson { Grade = "A" }, newState: RefluxPlayWatcher.PlayStateOff);

        Assert.Equal(1, resultCount);
    }

    [Fact]
    public void MenuOnly_WithoutPlay_RaisesNothing()
    {
        var watcher = NewWatcher();
        var started = 0;
        var result = 0;
        watcher.PlayStarted += (_, _) => started++;
        watcher.PlayResultDetected += (_, _) => result++;

        using var harness = new RefluxTestHarness(watcher);
        harness.SetPlayState("menu");

        Assert.Equal(0, started);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Start_NonExistentDirectory_Throws()
    {
        var watcher = NewWatcher();
        Assert.Throws<DirectoryNotFoundException>(
            () => watcher.Start(Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N"))));
    }
}
