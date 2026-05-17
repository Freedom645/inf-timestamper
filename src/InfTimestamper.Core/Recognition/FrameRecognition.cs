namespace InfTimestamper.Core.Recognition;

public sealed record FrameRecognition(
    DateTimeOffset CapturedAt,
    RecognizedState State,
    HashMatchResult? StateMatch,
    IReadOnlyDictionary<string, string> Fields);

public static class RecognitionFieldKeys
{
    public const string Title = "title";
    public const string DiffShort = "diff_s";
    public const string DiffLong = "diff_l";
    public const string Level = "level";
    public const string MissCount = "miss_count";
    public const string ExScore = "ex_score";
    public const string DjLevel = "dj_level";
    public const string Lamp = "lamp";
}

public static class RecognitionRoiKeys
{
    /// <summary>
    /// 難易度文字（NORMAL/HYPER/ANOTHER 等）の色判定用 ROI キー。
    /// rois.json で `"difficulty_color": [x, y, w, h]` 形式で指定する。
    /// </summary>
    public const string DifficultyColor = "difficulty_color";
}
