using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public sealed class RecognitionPipeline
{
    private static readonly string[] SelectionKeys =
    {
        RecognitionFieldKeys.Title,
        RecognitionFieldKeys.DiffShort,
        RecognitionFieldKeys.DiffLong,
        RecognitionFieldKeys.Level,
    };

    private readonly FrameRecognizer _recognizer;
    private readonly ILogger<RecognitionPipeline> _logger;
    private readonly Dictionary<string, string> _selectionFields = new();
    private RecognizedState _currentState = RecognizedState.Unknown;

    public RecognitionPipeline(FrameRecognizer recognizer, ILogger<RecognitionPipeline>? logger = null)
    {
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _logger = logger ?? NullLogger<RecognitionPipeline>.Instance;
    }

    public RecognizedState CurrentState => _currentState;

    public IReadOnlyDictionary<string, string> PendingSelection => _selectionFields;

    public event EventHandler<RecognitionStateChangedEventArgs>? StateChanged;
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public FrameRecognition ProcessFrame(ObsScreenshot screenshot)
    {
        var rec = _recognizer.Recognize(screenshot);
        HandleRecognition(rec);
        return rec;
    }

    public FrameRecognition ProcessFrame(Mat normalizedFrame, DateTimeOffset capturedAt)
    {
        var rec = _recognizer.RecognizeFrame(normalizedFrame, capturedAt);
        HandleRecognition(rec);
        return rec;
    }

    public void Reset()
    {
        _selectionFields.Clear();
        _currentState = RecognizedState.Unknown;
    }

    // テストや上位の事前認識済みフレームの注入用
    internal void InjectRecognition(FrameRecognition rec) => HandleRecognition(rec);

    private void HandleRecognition(FrameRecognition rec)
    {
        var oldState = _currentState;
        var transitioned = rec.State != oldState;

        if (transitioned)
        {
            _currentState = rec.State;

            // SongSelect への遷移時は前曲の保持データをクリア
            if (rec.State == RecognizedState.SongSelect)
                _selectionFields.Clear();

            StateChanged?.Invoke(this, new RecognitionStateChangedEventArgs(oldState, rec.State));
            _logger.LogDebug("認識状態遷移: {Old} → {New}", oldState, rec.State);
        }

        // SongSelect / PlayStart フレームで選曲情報を継続蓄積
        if (rec.State == RecognizedState.SongSelect || rec.State == RecognizedState.PlayStart)
            UpdateSelectionFields(rec.Fields);

        if (!transitioned) return;

        if (rec.State == RecognizedState.PlayStart)
        {
            var merged = MergeSelectionAndCurrent(rec.Fields);
            PlayStarted?.Invoke(this, new PlayStartedEventArgs(rec.CapturedAt, merged));
        }
        else if (rec.State == RecognizedState.Result)
        {
            PlayResultDetected?.Invoke(this, new PlayResultEventArgs(rec.CapturedAt, rec.Fields));
        }
    }

    private void UpdateSelectionFields(IReadOnlyDictionary<string, string> fields)
    {
        foreach (var key in SelectionKeys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                _selectionFields[key] = value;
        }
    }

    private IReadOnlyDictionary<string, string> MergeSelectionAndCurrent(IReadOnlyDictionary<string, string> current)
    {
        var merged = new Dictionary<string, string>(_selectionFields);
        foreach (var (k, v) in current)
        {
            if (!string.IsNullOrEmpty(v)) merged[k] = v;
        }
        return merged;
    }
}

public sealed class RecognitionStateChangedEventArgs : EventArgs
{
    public RecognitionStateChangedEventArgs(RecognizedState oldState, RecognizedState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public RecognizedState OldState { get; }
    public RecognizedState NewState { get; }
}

public sealed class PlayStartedEventArgs : EventArgs
{
    public PlayStartedEventArgs(DateTimeOffset capturedAt, IReadOnlyDictionary<string, string> fields)
    {
        CapturedAt = capturedAt;
        Fields = fields;
    }

    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}

public sealed class PlayResultEventArgs : EventArgs
{
    public PlayResultEventArgs(DateTimeOffset capturedAt, IReadOnlyDictionary<string, string> fields)
    {
        CapturedAt = capturedAt;
        Fields = fields;
    }

    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}
