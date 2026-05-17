using System.Text.RegularExpressions;

namespace InfTimestamper.Core.Formatting;

public static class FormatExpander
{
    public const string TimestampKey = "timestamp";

    public static readonly IReadOnlyList<string> SupportedKeys = new[]
    {
        TimestampKey,
        "title",
        "diff_l",
        "diff_s",
        "level",
        "miss_count",
        "ex_score",
        "dj_level",
        "lamp",
    };

    private static readonly Regex Pattern = new(@"\$([a-z_][a-z0-9_]*)", RegexOptions.Compiled);

    public static string Expand(string? format, IReadOnlyDictionary<string, string>? fields)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;
        if (fields is null || fields.Count == 0)
            return Pattern.Replace(format, string.Empty);

        return Pattern.Replace(format, match =>
        {
            var key = match.Groups[1].Value;
            return fields.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        });
    }

    public static string FormatTimestamp(TimeSpan relative)
    {
        var sign = relative < TimeSpan.Zero ? "-" : string.Empty;
        var abs = relative < TimeSpan.Zero ? -relative : relative;
        var hours = (long)abs.TotalHours;
        return $"{sign}{hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
    }
}
