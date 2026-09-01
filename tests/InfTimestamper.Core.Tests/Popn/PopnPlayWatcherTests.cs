using InfTimestamper.Core.Games;
using InfTimestamper.Core.Popn;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Popn;

public class PopnPlayWatcherTests
{
    private static PopnResultJson MakeResult(string? time = null, int score = 93578) => new()
    {
        Time = time,
        IdentifiedBy = "record-diff",
        Score = score,
        RankName = "AA",
        MedalName = "銅星",
        Judge = new PopnJudgeJson { Bad = 2 },
        Music = new PopnMusicJson { Title = "Sorrows", Sheet = "NORMAL", Level = 29 },
    };

    [Fact]
    public void EnterPlaying_FiresPlayStartedWithEmptyFields()
    {
        var watcher = new PopnPlayWatcher();
        PlayStartedEventArgs? started = null;
        watcher.PlayStarted += (_, e) => started = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();

        Assert.NotNull(started);
        // pop'n はプレイ開始時点で曲情報を出力しない（result.json 到着まで判らない）
        Assert.Empty(started!.Fields);
    }

    [Fact]
    public void SameState_RepeatedNotifications_AreNoOp()
    {
        var watcher = new PopnPlayWatcher();
        var startedCount = 0;
        watcher.PlayStarted += (_, _) => startedCount++;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.SetState(PopnPlayWatcher.StatePlaying);
        harness.SetState(PopnPlayWatcher.StatePlaying);

        Assert.Equal(1, startedCount);
    }

    [Fact]
    public void NonPlayingTransitions_DoNotFirePlayStarted()
    {
        var watcher = new PopnPlayWatcher();
        var startedCount = 0;
        watcher.PlayStarted += (_, _) => startedCount++;

        using var harness = new PopnTestHarness(watcher);
        harness.SetState(PopnPlayWatcher.StateMusicSelect);
        harness.SetState(PopnPlayWatcher.StatePlayEnded);
        harness.SetState(PopnPlayWatcher.StateIdle);

        Assert.Equal(0, startedCount);
    }

    [Fact]
    public void LeavingPlay_DoesNotFireResultUntilResultJsonArrives()
    {
        // result.json はリザルト画面の表示から数秒遅れて書かれるため、
        // 状態遷移のエッジではリザルトを発火しない
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.LeavePlay();

        Assert.Null(result);
    }

    [Fact]
    public void ResultJsonAfterPlay_FiresPlayResultWithFields()
    {
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.LeavePlay();
        harness.WriteResult(MakeResult(time: Now()));

        Assert.NotNull(result);
        Assert.Equal("Sorrows", result!.Fields["title"]);
        Assert.Equal("AA", result.Fields["rank"]);
        Assert.Equal("銅星", result.Fields["medal"]);
        Assert.Equal("93578", result.Fields["score"]);
        Assert.Equal("2", result.Fields["bad"]);
    }

    [Fact]
    public void ResultJsonWhileStillPlaying_IsAccepted()
    {
        // 遅延によりリザルトが「プレイ中」表示のうちに書かれることもありうる。
        // プレイ開始後であれば採用する。
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.WriteResult(MakeResult(time: Now()));

        Assert.NotNull(result);
    }

    [Fact]
    public void StaleResultJson_FromPreviousPlay_IsIgnored()
    {
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        // 前回プレイのリザルト（10 分前）が何らかの理由で再通知された場合
        harness.WriteResult(MakeResult(time: Now(TimeSpan.FromMinutes(-10))));

        Assert.Null(result);
    }

    [Fact]
    public void ResultJson_WithoutPlayStart_IsIgnored()
    {
        // 起動直後に残っている result.json を拾わない
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.WriteResult(MakeResult(time: Now()));

        Assert.Null(result);
    }

    [Fact]
    public void ResultJson_FiresOncePerPlay()
    {
        var watcher = new PopnPlayWatcher();
        var count = 0;
        watcher.PlayResultDetected += (_, _) => count++;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.LeavePlay();
        harness.WriteResult(MakeResult(time: Now()));
        // 同一 result.json への再通知（FileSystemWatcher の連続発火）は無視される
        harness.WriteResult(MakeResult(time: Now()));

        Assert.Equal(1, count);
    }

    [Fact]
    public void TwoConsecutivePlays_EachGetTheirOwnResult()
    {
        var watcher = new PopnPlayWatcher();
        var starts = 0;
        var results = new List<PlayResultEventArgs>();
        watcher.PlayStarted += (_, _) => starts++;
        watcher.PlayResultDetected += (_, e) => results.Add(e);

        using var harness = new PopnTestHarness(watcher);

        harness.EnterPlay();
        harness.LeavePlay();
        harness.WriteResult(MakeResult(time: Now(), score: 11111));

        harness.SetState(PopnPlayWatcher.StatePlaying);
        harness.LeavePlay();
        harness.WriteResult(MakeResult(time: Now(), score: 22222));

        Assert.Equal(2, starts);
        Assert.Equal(2, results.Count);
        Assert.Equal("11111", results[0].Fields["score"]);
        Assert.Equal("22222", results[1].Fields["score"]);
    }

    [Fact]
    public void ResultJson_WithoutTime_IsAccepted()
    {
        // time が読めないときは判定材料が無いので採用する
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.WriteResult(MakeResult(time: null));

        Assert.NotNull(result);
    }

    [Fact]
    public void BrokenResultJson_IsIgnoredWithoutThrowing()
    {
        var watcher = new PopnPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        using var harness = new PopnTestHarness(watcher);
        harness.EnterPlay();
        harness.WriteRawResult("{ this is not json");

        Assert.Null(result);
    }

    [Fact]
    public void Start_MissingDirectory_Throws()
    {
        var watcher = new PopnPlayWatcher();
        Assert.Throws<DirectoryNotFoundException>(
            () => watcher.Start(Path.Combine(Path.GetTempPath(), "popn-missing-" + Guid.NewGuid().ToString("N"))));
        Assert.False(watcher.IsRunning);
    }

    [Fact]
    public async Task StartAndStop_TogglesIsRunning()
    {
        using var dir = new TempDirectory();
        var watcher = new PopnPlayWatcher();
        await using (watcher)
        {
            watcher.Start(dir.Path);
            Assert.True(watcher.IsRunning);

            await watcher.StopAsync();
            Assert.False(watcher.IsRunning);
        }
    }

    private static string Now(TimeSpan? offset = null)
        => DateTimeOffset.Now.Add(offset ?? TimeSpan.Zero).ToString("yyyy-MM-dd HH:mm:ss");
}
