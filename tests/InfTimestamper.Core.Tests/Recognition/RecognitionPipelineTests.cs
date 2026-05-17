using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class RecognitionPipelineTests
{
    private static readonly Dictionary<string, string> Empty = new();

    private static RecognitionPipeline NewPipeline() => new(
        new FrameRecognizer(new ImageHasher(), new NoOpOcrService(), HashResource.Empty(), RoiResource.Empty()));

    private static FrameRecognition Frame(RecognizedState state, DateTimeOffset at, Dictionary<string, string>? fields = null)
        => new(at, state, null, fields ?? Empty);

    [Fact]
    public void Inject_FirstFrame_FiresStateChanged()
    {
        var pipe = NewPipeline();
        var transitions = new List<RecognitionStateChangedEventArgs>();
        pipe.StateChanged += (_, e) => transitions.Add(e);

        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, DateTimeOffset.Now));

        Assert.Single(transitions);
        Assert.Equal(RecognizedState.Unknown, transitions[0].OldState);
        Assert.Equal(RecognizedState.SongSelect, transitions[0].NewState);
        Assert.Equal(RecognizedState.SongSelect, pipe.CurrentState);
    }

    [Fact]
    public void Inject_SameStateRepeatedly_DoesNotRefireStateChanged()
    {
        var pipe = NewPipeline();
        int stateChangedCount = 0;
        pipe.StateChanged += (_, _) => stateChangedCount++;

        var t = DateTimeOffset.Now;
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t));
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t.AddSeconds(1)));
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t.AddSeconds(2)));

        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public void Inject_SongSelectThenPlayStart_PlayStartedReceivesMergedFields()
    {
        var pipe = NewPipeline();
        PlayStartedEventArgs? captured = null;
        pipe.PlayStarted += (_, e) => captured = e;

        var t = DateTimeOffset.Now;

        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t, new Dictionary<string, string>
        {
            [RecognitionFieldKeys.Title] = "Test Song",
            [RecognitionFieldKeys.DiffShort] = "SPA",
            [RecognitionFieldKeys.Level] = "11",
        }));

        // 連続フレーム（追加情報を蓄積）
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t.AddSeconds(1), new Dictionary<string, string>
        {
            [RecognitionFieldKeys.DiffLong] = "ANOTHER",
        }));

        // PlayStart エッジ
        var startAt = t.AddSeconds(5);
        pipe.InjectRecognition(Frame(RecognizedState.PlayStart, startAt));

        Assert.NotNull(captured);
        Assert.Equal(startAt, captured!.CapturedAt);
        Assert.Equal("Test Song", captured.Fields[RecognitionFieldKeys.Title]);
        Assert.Equal("SPA", captured.Fields[RecognitionFieldKeys.DiffShort]);
        Assert.Equal("ANOTHER", captured.Fields[RecognitionFieldKeys.DiffLong]);
        Assert.Equal("11", captured.Fields[RecognitionFieldKeys.Level]);
    }

    [Fact]
    public void Inject_PlayStartToResult_FiresPlayResultDetected()
    {
        var pipe = NewPipeline();
        PlayResultEventArgs? result = null;
        pipe.PlayResultDetected += (_, e) => result = e;

        var t = DateTimeOffset.Now;
        pipe.InjectRecognition(Frame(RecognizedState.PlayStart, t));
        pipe.InjectRecognition(Frame(RecognizedState.Result, t.AddSeconds(60), new Dictionary<string, string>
        {
            [RecognitionFieldKeys.MissCount] = "3",
            [RecognitionFieldKeys.DjLevel] = "AAA",
            [RecognitionFieldKeys.Lamp] = "FC",
            [RecognitionFieldKeys.ExScore] = "1234",
        }));

        Assert.NotNull(result);
        Assert.Equal("3", result!.Fields[RecognitionFieldKeys.MissCount]);
        Assert.Equal("AAA", result.Fields[RecognitionFieldKeys.DjLevel]);
        Assert.Equal("FC", result.Fields[RecognitionFieldKeys.Lamp]);
        Assert.Equal("1234", result.Fields[RecognitionFieldKeys.ExScore]);
    }

    [Fact]
    public void Inject_ResultToSongSelect_ClearsPendingSelection()
    {
        var pipe = NewPipeline();

        var t = DateTimeOffset.Now;
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t, new Dictionary<string, string>
        {
            [RecognitionFieldKeys.Title] = "OldSong",
        }));
        pipe.InjectRecognition(Frame(RecognizedState.PlayStart, t.AddSeconds(5)));
        pipe.InjectRecognition(Frame(RecognizedState.Result, t.AddMinutes(2)));

        // 次曲の選曲中に遷移したら前曲の保持はクリアされる
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, t.AddMinutes(2).AddSeconds(5)));

        Assert.Empty(pipe.PendingSelection);
    }

    [Fact]
    public void Inject_PlayStartWithoutPriorSongSelect_ReusesCurrentFrameFields()
    {
        var pipe = NewPipeline();
        PlayStartedEventArgs? captured = null;
        pipe.PlayStarted += (_, e) => captured = e;

        var t = DateTimeOffset.Now;
        pipe.InjectRecognition(Frame(RecognizedState.PlayStart, t, new Dictionary<string, string>
        {
            [RecognitionFieldKeys.Title] = "Direct",
        }));

        Assert.NotNull(captured);
        Assert.Equal("Direct", captured!.Fields[RecognitionFieldKeys.Title]);
    }

    [Fact]
    public void Reset_ClearsStateAndPendingSelection()
    {
        var pipe = NewPipeline();
        pipe.InjectRecognition(Frame(RecognizedState.SongSelect, DateTimeOffset.Now, new Dictionary<string, string>
        {
            [RecognitionFieldKeys.Title] = "X",
        }));

        pipe.Reset();

        Assert.Empty(pipe.PendingSelection);
        Assert.Equal(RecognizedState.Unknown, pipe.CurrentState);
    }
}
