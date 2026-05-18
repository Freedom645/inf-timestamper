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

        // 要件: min(3, ceil(length * 0.3))。装飾フォントの誤読を吸収するには tight すぎる感もあるが、
        // 緩めると別曲への誤マッチが増えるため保守的にしておく。OCR 精度向上は別途取り組む。
        // ただし極端に短い OCR 結果 (1-2 文字) は DB 内の短いタイトル "V" などへ容易に誤マッチするため
        // Confirmed (distance=0) のみ許容する。
        var threshold = normalized.Length <= 2
            ? 0
            : Math.Min(3, (int)Math.Ceiling(normalized.Length * 0.3));

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
