namespace InfTimestamper.Core.Recognition;

public sealed record SongRecord(
    string Id,
    string Title,
    string TitleNormalized,
    IReadOnlyDictionary<string, int> Charts);
