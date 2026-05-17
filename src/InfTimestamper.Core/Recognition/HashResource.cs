namespace InfTimestamper.Core.Recognition;

public sealed record StateHashEntry(string Name, Roi Roi, ulong Ahash, int Threshold);

public sealed record IconHashEntry(string Value, Roi Roi, ulong Ahash, int Threshold);

public sealed class HashResource
{
    public const int DefaultThreshold = 10;

    public IReadOnlyDictionary<string, IReadOnlyList<StateHashEntry>> States { get; init; }
        = new Dictionary<string, IReadOnlyList<StateHashEntry>>();

    public IReadOnlyList<IconHashEntry> Difficulty { get; init; } = Array.Empty<IconHashEntry>();
    public IReadOnlyList<IconHashEntry> DjLevel { get; init; } = Array.Empty<IconHashEntry>();
    public IReadOnlyList<IconHashEntry> Lamp { get; init; } = Array.Empty<IconHashEntry>();

    public static HashResource Empty() => new();

    public bool IsEmpty =>
        States.Count == 0 && Difficulty.Count == 0 && DjLevel.Count == 0 && Lamp.Count == 0;
}
