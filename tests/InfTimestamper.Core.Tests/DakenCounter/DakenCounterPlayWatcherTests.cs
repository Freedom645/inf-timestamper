using System.Text.Json;
using InfTimestamper.Core.DakenCounter;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.DakenCounter;

/// <summary>
/// WebSocket 接続を張らず、受信済みメッセージを <c>HandleMessage</c> へ受信時刻つきで直接流して検知ロジックを検証する。
/// </summary>
public class DakenCounterPlayWatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 21, 0, 0, TimeSpan.FromHours(9));

    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static string Graph(int playcount) => $$$"""{"type":"graph","data":{"playcount":{{{playcount}}}}}""";

    private static string Option() => """{"type":"option","data":{"option":"RANDOM","gauge":"exh"}}""";

    private static string TodayStats() => """{"type":"today_stats","data":{"playcount":1}}""";

    private static string CurrentSong(string music, string difficulty = "SPA", string lv = "11")
        => $$$"""{"type":"history_cursong","data":{"music":"{{{music}}}","difficulty":"{{{difficulty}}}","lv":"{{{lv}}}"}}""";

    /// <summary>today_updates（本日分の全件）。<paramref name="items"/> は新しい順でなくてよい。</summary>
    private static string TodayUpdates(params (string Title, int Score, DateTimeOffset At)[] items)
    {
        var entries = items.Select(i =>
            $$"""{"chart_id":"{{i.Title}}","title":"{{i.Title}}","difficulty":"SPA","lv":"11","notes":1000,"score":{{i.Score}},"bp":5,"dead":false,"lamp":5,"timestamp":{{i.At.ToUnixTimeSeconds()}}}""");
        return $$$"""{"type":"today_updates","data":{"start_time":0,"items":[{{{string.Join(",", entries)}}}]}}""";
    }

    private sealed class Recorder
    {
        public Recorder(DakenCounterPlayWatcher watcher)
        {
            watcher.PlayStarted += (_, e) => Started.Add(e);
            watcher.PlayResultDetected += (_, e) => Results.Add(e);
        }

        public List<PlayStartedEventArgs> Started { get; } = new();
        public List<PlayResultEventArgs> Results { get; } = new();
    }

    /// <summary>接続直後のキャッシュ再送（graph で基準を取る）まで済ませたウォッチャー。</summary>
    private static (DakenCounterPlayWatcher Watcher, Recorder Recorder) Connected(int playcount = 0)
    {
        var watcher = new DakenCounterPlayWatcher();
        var recorder = new Recorder(watcher);
        watcher.OnConnected(At(0));
        watcher.HandleMessage(Graph(playcount), At(0.01));
        return (watcher, recorder);
    }

    [Fact]
    public void GraphOnConnect_OnlyTakesBaseline()
    {
        // 接続直後に再送される graph（本日のプレイ回数ぶん）はプレイ終了ではない
        var (_, recorder) = Connected(playcount: 12);

        Assert.Empty(recorder.Started);
    }

    [Fact]
    public void PlaycountIncrease_FiresPlayStartedAtLastFeedBeforeThePlay()
    {
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(CurrentSong("Song A"), At(10));
        watcher.HandleMessage(CurrentSong("Song A"), At(12)); // 曲決定画面まで流れる
        // ---- プレイ中は何も飛ばない ----
        watcher.HandleMessage(Graph(1), At(130));

        var started = Assert.Single(recorder.Started);
        Assert.Equal(At(12), started.CapturedAt);
        Assert.Equal("Song A", started.Fields[FieldKeys.Title]);
        Assert.Equal("SPA", started.Fields[FieldKeys.DiffShort]);
        Assert.Equal("11", started.Fields[FieldKeys.Level]);
    }

    [Fact]
    public void GraphWithSamePlaycount_IsIgnored()
    {
        // 打鍵カウンタの設定変更でも graph が飛ぶが、playcount は変わらない
        var (watcher, recorder) = Connected(playcount: 3);

        watcher.HandleMessage(Graph(3), At(30));

        Assert.Empty(recorder.Started);
    }

    [Fact]
    public void PlaycountDecrease_RebaselinesWithoutFiring()
    {
        var (watcher, recorder) = Connected(playcount: 30);

        watcher.HandleMessage(Graph(0), At(30)); // 日付が変わって本日分がリセットされた
        watcher.HandleMessage(Graph(1), At(150));

        var started = Assert.Single(recorder.Started);
        Assert.Equal(At(30), started.CapturedAt);
    }

    [Fact]
    public void QuickRetry_EachAttemptStartsAtPreviousAttemptEnd()
    {
        // クイックリトライはリザルト画面も選曲画面も通らないので、graph と today_stats しか飛ばない
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(CurrentSong("Song A"), At(10));
        watcher.HandleMessage(Graph(1), At(40));
        watcher.HandleMessage(TodayStats(), At(41));
        watcher.HandleMessage(Graph(2), At(75));
        watcher.HandleMessage(TodayStats(), At(76));
        watcher.HandleMessage(Graph(3), At(200));

        Assert.Equal(new[] { At(10), At(40), At(75) }, recorder.Started.Select(s => s.CapturedAt));
        Assert.All(recorder.Started, s => Assert.Equal("Song A", s.Fields[FieldKeys.Title]));
    }

    [Fact]
    public void NewTodayUpdatesAfterPlay_FiresResultWithLatestEntry()
    {
        var (watcher, recorder) = Connected();
        watcher.HandleMessage(TodayUpdates(("Old", 1500, At(-600))), At(0.02));

        watcher.HandleMessage(CurrentSong("Song A"), At(10));
        watcher.HandleMessage(Graph(1), At(130));
        watcher.HandleMessage(TodayUpdates(("Old", 1500, At(-600)), ("Song A", 1800, At(132))), At(135));

        var result = Assert.Single(recorder.Results);
        Assert.Equal("Song A", result.Fields[FieldKeys.Title]);
        Assert.Equal("1800", result.Fields[FieldKeys.ExScore]);
        Assert.Equal("HARD", result.Fields[FieldKeys.Lamp]);
    }

    [Fact]
    public void TodayUpdatesWhileNotAwaiting_IsIgnored()
    {
        // 接続直後に再送される最新分。プレイ終了を見ていないので採らない
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(-10))), At(0.02));

        Assert.Empty(recorder.Results);
    }

    [Fact]
    public void ResendOfSameResult_IsTakenOnlyOnce()
    {
        // BPIM2 の取得が終わるたびに同じ today_updates が再送される
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(Graph(1), At(130));
        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(132))), At(135));
        watcher.HandleMessage(Graph(2), At(300)); // リザルト画面からリトライ
        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(132))), At(301)); // 前のプレイの再送

        Assert.Single(recorder.Results);
    }

    [Fact]
    public void ResultOlderThanPlayStart_IsIgnored()
    {
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(Option(), At(100));
        watcher.HandleMessage(Graph(1), At(200));
        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(50))), At(205));

        Assert.Empty(recorder.Results);
    }

    [Fact]
    public void ResendBurst_DoesNotMovePlayStart()
    {
        // リザルト画面から即リトライすると、BPIM2 の再送（history_cursong + today_updates の組）が
        // プレイ開始の後に届く。これを最終受信とみなすとプレイ開始が遅れる
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(Graph(1), At(130));
        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(132))), At(135));
        watcher.HandleMessage(CurrentSong("Song A"), At(136)); // リザルト画面の読み取り（ここで即リトライ）
        // ---- プレイ開始 ----
        watcher.HandleMessage(CurrentSong("Song A"), At(137.95)); // BPIM2 の再送
        watcher.HandleMessage(TodayUpdates(("Song A", 1800, At(132))), At(138));
        watcher.HandleMessage(TodayStats(), At(139));
        watcher.HandleMessage(Graph(2), At(260));

        Assert.Equal(At(136), recorder.Started[^1].CapturedAt);
    }

    [Fact]
    public void TodayStats_IsNotTreatedAsFeed()
    {
        var (watcher, recorder) = Connected();

        watcher.HandleMessage(Option(), At(20));
        watcher.HandleMessage(TodayStats(), At(25));
        watcher.HandleMessage(Graph(1), At(150));

        Assert.Equal(At(20), Assert.Single(recorder.Started).CapturedAt);
    }

    [Fact]
    public void Reconnect_RetakesBaseline()
    {
        var (watcher, recorder) = Connected(playcount: 5);

        // 切断中に 2 プレイ進んでいた。再接続時の graph は基準取りで、プレイ終了ではない
        watcher.OnConnected(At(600));
        watcher.HandleMessage(Graph(7), At(600.01));
        watcher.HandleMessage(Graph(8), At(720));

        var started = Assert.Single(recorder.Started);
        Assert.Equal(At(600.01), started.CapturedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://127.0.0.1:8767")]
    [InlineData("not a url")]
    public void Start_InvalidEndpoint_Throws(string endpoint)
    {
        var watcher = new DakenCounterPlayWatcher();

        Assert.Throws<ArgumentException>(() => watcher.Start(WatchTarget.ForEndpoint(endpoint)));
        Assert.False(watcher.IsRunning);
    }

    // ---- 実機ダンプでの回帰テスト ----

    /// <summary>
    /// 実機ダンプ（約 3 時間）を受信時刻どおりに流し、同じ配信を Reflux で記録した結果と突き合わせる。
    /// ダンプは docs/sample/daken-counter/probe.jsonl（tools/daken-counter-probe.ps1 で採取し、使うフィールドだけに削ったもの）。
    /// </summary>
    private sealed class SampleReplay
    {
        public SampleReplay()
        {
            var watcher = new DakenCounterPlayWatcher();
            var recorder = new Recorder(watcher);

            foreach (var line in File.ReadLines(TestPaths.RepositoryFile("docs", "sample", "daken-counter", "probe.jsonl")))
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var receivedAt = DateTimeOffset.Parse(root.GetProperty("receivedAt").GetString()!);

                if (root.TryGetProperty("event", out var ev))
                {
                    if (ev.GetString() == "connected") watcher.OnConnected(receivedAt);
                    continue;
                }

                watcher.HandleMessage(root.GetProperty("message").GetRawText(), receivedAt);
            }

            Started = recorder.Started;
            Results = recorder.Results;

            using var record = JsonDocument.Parse(File.ReadAllText(
                TestPaths.RepositoryFile("docs", "sample", "daken-counter", "reflux-record.json")));
            Reflux = record.RootElement.GetProperty("timestamps").EnumerateArray()
                .Select(t => (
                    At: DateTimeOffset.Parse(t.GetProperty("playStartedAt").GetString()!),
                    Fields: t.GetProperty("fields").EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.ToString())))
                .ToList();
        }

        public List<PlayStartedEventArgs> Started { get; }
        public List<PlayResultEventArgs> Results { get; }
        public List<(DateTimeOffset At, Dictionary<string, string> Fields)> Reflux { get; }
    }

    [Fact]
    public void Sample_DetectsEveryPlayIncludingQuickRetries()
    {
        var replay = new SampleReplay();

        // graph の playcount 増加 89 回 = プレイ 89 回。Reflux（68 件）が拾えないクイックリトライも含む
        Assert.Equal(89, replay.Started.Count);
        // リザルト登録 50 件（BPIM2 による同内容の再送 198 回は除かれる）
        Assert.Equal(50, replay.Results.Count);
        Assert.True(replay.Started.Count > replay.Reflux.Count);
    }

    [Fact]
    public void Sample_PlayStartsMatchReflux()
    {
        var replay = new SampleReplay();

        // Reflux のプレイ開始（プレイ画面に入った時刻）に対して、0〜8 秒早い（曲決定画面〜ロード中のぶん）のが正常。
        // 遅れる側は 1.5 秒まで（BPIM2 の再送を外しきれなかったぶん）
        var unmatched = replay.Reflux
            .Where(r => !replay.Started.Any(s =>
                (r.At - s.CapturedAt) >= TimeSpan.FromSeconds(-1.5)
                && (r.At - s.CapturedAt) <= TimeSpan.FromSeconds(8.5)))
            .Select(r => r.At.ToString("HH:mm:ss"))
            .ToList();

        // 09:51:49 の判定 0 のアテンプト（打鍵カウンタは graph を出さない）の直後にクイックリトライした 1 件だけは、
        // 判定 0 のアテンプトの開始時刻（09:51:48）に吸われる。画像認識側の仕様なので許容する
        Assert.Equal(new[] { "09:52:08" }, unmatched);
    }

    /// <summary>
    /// リザルトに対応する Reflux の記録（同じ曲・同じ EX スコアで、リザルトより前の直近のもの）。
    /// Reflux はクイックリトライを見分けられず、最初のアテンプトの開始時刻のエントリに最後のアテンプトの成績を入れる
    /// （実機ダンプの 08:50:25 など）。そのため Reflux の時刻はリザルトの何分も前になりうる。
    /// </summary>
    private static (DateTimeOffset At, Dictionary<string, string> Fields) RefluxFor(SampleReplay replay, PlayResultEventArgs result)
        => replay.Reflux.Last(r => r.Fields["title"] == result.Fields[FieldKeys.Title]
                                   && r.Fields["ex_score"] == result.Fields[FieldKeys.ExScore]
                                   && r.At < result.CapturedAt);

    [Fact]
    public void Sample_ResultsMatchReflux()
    {
        var replay = new SampleReplay();

        foreach (var result in replay.Results)
        {
            var reflux = RefluxFor(replay, result);

            Assert.Equal(reflux.Fields["diff_s"], result.Fields[FieldKeys.DiffShort]);
            Assert.Equal(reflux.Fields["diff_l"], result.Fields[FieldKeys.DiffLong]);
            Assert.Equal(reflux.Fields["level"], result.Fields[FieldKeys.Level]);
            Assert.Equal(reflux.Fields["lamp"], result.Fields[FieldKeys.Lamp]);
            Assert.Equal(reflux.Fields["dj_level"], result.Fields[FieldKeys.DjLevel]);
            // BP は途中落ちのとき打鍵カウンタ（残りノーツも POOR に数える）と Reflux で食い違うので、完走したものだけ比べる
            if (reflux.Fields["lamp"] != "FAILED")
                Assert.Equal(reflux.Fields["miss_count"], result.Fields[FieldKeys.MissCount]);
        }
    }

    [Fact]
    public void Sample_EachResultMergesIntoTheEntryOfThatPlay()
    {
        // リザルトは直近のプレイ開始エントリにマージされる（MainWindowViewModel）。
        // その直近エントリが、リザルトと同じプレイのものであること:
        // Reflux で同じプレイが始まった後に始まっていて、そこからリザルトまでの間に別のプレイが始まっていない
        var replay = new SampleReplay();

        foreach (var result in replay.Results)
        {
            var latestStart = replay.Started.Last(s => s.CapturedAt < result.CapturedAt).CapturedAt;
            var reflux = RefluxFor(replay, result);

            Assert.True(latestStart >= reflux.At - TimeSpan.FromSeconds(8.5),
                $"{result.Fields[FieldKeys.Title]} のリザルトが、それより前のプレイ（{latestStart:HH:mm:ss}）に付いています。");
            Assert.DoesNotContain(replay.Reflux, r =>
                r.At > latestStart + TimeSpan.FromSeconds(1.5) && r.At < result.CapturedAt && r.At != reflux.At);
        }
    }
}
