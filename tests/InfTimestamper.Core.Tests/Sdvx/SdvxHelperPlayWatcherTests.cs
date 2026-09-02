using InfTimestamper.Core.Games;
using InfTimestamper.Core.Sdvx;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Sdvx;

/// <summary>
/// WebSocket 接続を張らず、受信済みメッセージを <c>HandleMessage</c> へ直接流して検知ロジックを検証する。
/// </summary>
public class SdvxHelperPlayWatcherTests
{
    [Fact]
    public void SongDecidedNowPlaying_FiresPlayStartedWithSongInfo()
    {
        var watcher = new SdvxHelperPlayWatcher();
        PlayStartedEventArgs? started = null;
        watcher.PlayStarted += (_, e) => started = e;

        watcher.HandleMessage(SdvxMessages.Utf8(
            SdvxMessages.NowPlayingFromSongDecided("999", "MXM", 20)));

        Assert.NotNull(started);
        Assert.Equal("999", started!.Fields["title"]);
        Assert.Equal("MXM", started.Fields["diff_s"]);
        Assert.Equal("20", started.Fields["level"]);
    }

    [Fact]
    public void SelectScreenNowPlaying_DoesNotFirePlayStarted()
    {
        // 選曲画面はカーソルを動かすたびに nowplaying が飛ぶので、プレイ開始として扱ってはいけない
        var watcher = new SdvxHelperPlayWatcher();
        var startedCount = 0;
        watcher.PlayStarted += (_, _) => startedCount++;

        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSelect()));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSelect("Another Song")));

        Assert.Equal(0, startedCount);
    }

    [Fact]
    public void TodayResults_AfterPlayStart_FiresPlayResult()
    {
        var watcher = new SdvxHelperPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSongDecided()));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.TodayResults(
            SdvxMessages.ResultItem(DateTimeOffset.Now.AddMinutes(2)))));

        Assert.NotNull(result);
        Assert.Equal("9765432", result!.Fields["score"]);
        Assert.Equal("UC", result.Fields["clear_lamp"]);
    }

    [Fact]
    public void TodayResults_WithoutPlayStart_IsIgnored()
    {
        // 接続直後に本日分がまとめて飛んでくるため、リザルト待ちでないときは無視する
        var watcher = new SdvxHelperPlayWatcher();
        var resultCount = 0;
        watcher.PlayResultDetected += (_, _) => resultCount++;

        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.TodayResults(
            SdvxMessages.ResultItem(DateTimeOffset.Now))));

        Assert.Equal(0, resultCount);
    }

    [Fact]
    public void TodayResults_OlderThanPlayStart_IsIgnored()
    {
        // 前のプレイのリザルトを取り違えない
        var watcher = new SdvxHelperPlayWatcher();
        var resultCount = 0;
        watcher.PlayResultDetected += (_, _) => resultCount++;

        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSongDecided()));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.TodayResults(
            SdvxMessages.ResultItem(DateTimeOffset.Now.AddMinutes(-5)))));

        Assert.Equal(0, resultCount);
    }

    [Fact]
    public void TodayResults_PicksTheNewestItem()
    {
        var watcher = new SdvxHelperPlayWatcher();
        PlayResultEventArgs? result = null;
        watcher.PlayResultDetected += (_, e) => result = e;

        var now = DateTimeOffset.Now;
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSongDecided()));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.TodayResults(
            SdvxMessages.ResultItem(now.AddMinutes(-10), title: "Old Song"),
            SdvxMessages.ResultItem(now.AddMinutes(2), title: "New Song"),
            SdvxMessages.ResultItem(now.AddMinutes(-5), title: "Middle Song"))));

        Assert.NotNull(result);
        Assert.Equal("New Song", result!.Fields["title"]);
    }

    [Fact]
    public void TodayResults_ConsumesTheAwaitingState()
    {
        // 1 プレイにつきリザルトは 1 回。選曲画面での再配信を二重に拾わない
        var watcher = new SdvxHelperPlayWatcher();
        var resultCount = 0;
        watcher.PlayResultDetected += (_, _) => resultCount++;

        var results = SdvxMessages.TodayResults(SdvxMessages.ResultItem(DateTimeOffset.Now.AddMinutes(2)));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSongDecided()));
        watcher.HandleMessage(SdvxMessages.Utf8(results));
        watcher.HandleMessage(SdvxMessages.Utf8(results));

        Assert.Equal(1, resultCount);
    }

    [Fact]
    public void EmptyTodayResults_IsIgnored()
    {
        var watcher = new SdvxHelperPlayWatcher();
        var resultCount = 0;
        watcher.PlayResultDetected += (_, _) => resultCount++;

        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.NowPlayingFromSongDecided()));
        watcher.HandleMessage(SdvxMessages.Utf8(SdvxMessages.TodayResults()));

        Assert.Equal(0, resultCount);
    }

    [Theory]
    [InlineData("""{"type":"hello","time":1.0}""")]
    [InlineData("""{"type":"heartbeat","data":{"time":1.0}}""")]
    [InlineData("""{"type":"cursong","data":{"title":"x"}}""")]
    [InlineData("""{"type":"vf","data":{}}""")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    public void UnrelatedMessages_AreIgnoredWithoutThrowing(string message)
    {
        var watcher = new SdvxHelperPlayWatcher();
        var events = 0;
        watcher.PlayStarted += (_, _) => events++;
        watcher.PlayResultDetected += (_, _) => events++;

        watcher.HandleMessage(SdvxMessages.Utf8(message));

        Assert.Equal(0, events);
    }

    [Fact]
    public void Start_RejectsANonWebSocketEndpoint()
    {
        var watcher = new SdvxHelperPlayWatcher();

        Assert.Throws<ArgumentException>(() => watcher.Start(string.Empty));
        Assert.Throws<ArgumentException>(() => watcher.Start(@"C:\sdvx_helper\out"));
        Assert.Throws<ArgumentException>(() => watcher.Start("http://127.0.0.1:8767"));
        Assert.False(watcher.IsRunning);
    }
}
