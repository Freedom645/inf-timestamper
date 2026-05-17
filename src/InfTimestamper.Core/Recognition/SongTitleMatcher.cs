namespace InfTimestamper.Core.Recognition;

public enum SongMatchKind
{
    Confirmed,
    Candidate,
    Unmatched,
}

public sealed record SongMatchResult(
    SongRecord? Record,
    string Title,
    SongMatchKind Kind,
    int? Distance);

public sealed class SongTitleMatcher
{
    private readonly SongRepository _repository;

    public SongTitleMatcher(SongRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public SongMatchResult Match(string? ocrOutput)
    {
        var rawTitle = ocrOutput ?? string.Empty;
        var normalized = TitleNormalizer.Normalize(rawTitle);
        if (normalized.Length == 0)
            return new SongMatchResult(null, rawTitle, SongMatchKind.Unmatched, null);

        var threshold = Math.Min(3, (int)Math.Ceiling(normalized.Length * 0.3));

        SongRecord? best = null;
        int bestDistance = int.MaxValue;
        foreach (var record in _repository.All)
        {
            if (string.IsNullOrEmpty(record.TitleNormalized)) continue;

            var d = LevenshteinDistance.Compute(normalized, record.TitleNormalized);
            if (d == 0)
                return new SongMatchResult(record, record.Title, SongMatchKind.Confirmed, 0);

            if (d < bestDistance)
            {
                bestDistance = d;
                best = record;
            }
        }

        if (best is null || bestDistance > threshold)
            return new SongMatchResult(null, rawTitle, SongMatchKind.Unmatched, best is null ? null : bestDistance);

        return new SongMatchResult(best, best.Title, SongMatchKind.Candidate, bestDistance);
    }
}
