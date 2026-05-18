namespace InfTimestamper.Core.Recognition;

/// <summary>
/// アイコン照合に使うハッシュアルゴリズム。
/// aHash (Average) は速いが識別力が低く、似た文字 (例: A vs D) で衝突しがち。
/// pHash (Perceptual) は DCT ベースでより識別力が高く、DJ Level など細かな文字の判別に推奨。
/// </summary>
public enum HashAlgorithm
{
    Average,
    Perceptual,
}

public sealed record StateHashEntry(string Name, Roi Roi, ulong Ahash, int Threshold);

public sealed record IconHashEntry(
    string Value,
    Roi Roi,
    ulong Hash,
    int Threshold,
    HashAlgorithm Algo = HashAlgorithm.Average,
    PlaySide Side = PlaySide.Unknown);

public sealed class HashResource
{
    public const int DefaultThreshold = 10;

    public IReadOnlyDictionary<string, IReadOnlyList<StateHashEntry>> States { get; init; }
        = new Dictionary<string, IReadOnlyList<StateHashEntry>>();

    public IReadOnlyList<IconHashEntry> Difficulty { get; init; } = Array.Empty<IconHashEntry>();
    public IReadOnlyList<IconHashEntry> DjLevel { get; init; } = Array.Empty<IconHashEntry>();
    public IReadOnlyList<IconHashEntry> Lamp { get; init; } = Array.Empty<IconHashEntry>();
    public IReadOnlyList<IconHashEntry> PlayMode { get; init; } = Array.Empty<IconHashEntry>();

    public static HashResource Empty() => new();

    public bool IsEmpty =>
        States.Count == 0 && Difficulty.Count == 0 && DjLevel.Count == 0
        && Lamp.Count == 0 && PlayMode.Count == 0;
}
