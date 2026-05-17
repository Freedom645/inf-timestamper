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
