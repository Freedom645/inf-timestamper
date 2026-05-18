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

    /// <summary>
    /// SongSelect 入場直後 (= 状態遷移直後) の N フレームを認識用に信用しない安定化バッファ。
    /// 1 だと「入場フレームをスキップして 2 フレーム目以降を信用」する。INFINITAS のフェード遷移は
    /// 約 1Hz の取得周期内で完了するため 1 で十分なケースが多い。
    /// </summary>
    public const int SongSelectStabilityFrames = 1;

    private readonly FrameRecognizer _recognizer;
    private readonly ILogger<RecognitionPipeline> _logger;
    private readonly Dictionary<string, string> _selectionFields = new();
    private RecognizedState _currentState = RecognizedState.Unknown;
    private PlaySide _lastKnownSide = PlaySide.Unknown;
    private int _framesInCurrentState = 0;

    public RecognitionPipeline(FrameRecognizer recognizer, ILogger<RecognitionPipeline>? logger = null)
    {
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _logger = logger ?? NullLogger<RecognitionPipeline>.Instance;
    }

    public RecognizedState CurrentState => _currentState;

    public PlaySide LastKnownSide => _lastKnownSide;

    public IReadOnlyDictionary<string, string> PendingSelection => _selectionFields;

    public event EventHandler<RecognitionStateChangedEventArgs>? StateChanged;
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public FrameRecognition ProcessFrame(ObsScreenshot screenshot)
    {
        var rec = _recognizer.Recognize(screenshot, _lastKnownSide);
        HandleRecognition(rec);
        return rec;
    }

    public FrameRecognition ProcessFrame(Mat normalizedFrame, DateTimeOffset capturedAt)
    {
        var rec = _recognizer.RecognizeFrame(normalizedFrame, capturedAt, _lastKnownSide);
        HandleRecognition(rec);
        return rec;
    }

    public void Reset()
    {
        _selectionFields.Clear();
        _currentState = RecognizedState.Unknown;
        _lastKnownSide = PlaySide.Unknown;
        _framesInCurrentState = 0;
    }

    // テストや上位の事前認識済みフレームの注入用
    internal void InjectRecognition(FrameRecognition rec) => HandleRecognition(rec);

    private void HandleRecognition(FrameRecognition rec)
    {
        // 現フレームで side が検出できた場合は更新（SongSelect の矢印アイコンで判明する）。
        // 検出不能なフレーム（Result など）では従前の last known side を維持する。
        if (rec.DetectedSide != PlaySide.Unknown && rec.DetectedSide != _lastKnownSide)
        {
            _logger.LogDebug("プレイサイド更新: {Old} → {New}", _lastKnownSide, rec.DetectedSide);
            _lastKnownSide = rec.DetectedSide;
        }

        var oldState = _currentState;
        var transitioned = rec.State != oldState;

        if (transitioned)
        {
            _currentState = rec.State;
            _framesInCurrentState = 1; // 入場フレーム = 1 フレーム目

            // SongSelect への遷移時は前曲の保持データをクリア
            if (rec.State == RecognizedState.SongSelect)
                _selectionFields.Clear();

            StateChanged?.Invoke(this, new RecognitionStateChangedEventArgs(oldState, rec.State));
            _logger.LogDebug("認識状態遷移: {Old} → {New}", oldState, rec.State);
        }
        else
        {
            _framesInCurrentState++;
        }

        // SongSelect では入場直後の N フレーム (画面遷移中) を破棄し、安定後のフィールドだけ蓄積。
        // 遷移直後は OCR ROI に前画面の残像やフェードイン途中の中途半端な文字が混入することが多い。
        // PlayStart はそもそも FrameRecognizer が fields を抽出しないので影響なし。
        if (rec.State == RecognizedState.SongSelect
            && _framesInCurrentState > SongSelectStabilityFrames)
        {
            UpdateSelectionFields(rec.Fields);
        }

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
