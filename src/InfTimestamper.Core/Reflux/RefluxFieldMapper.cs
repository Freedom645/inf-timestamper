using System.Globalization;
using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Reflux;

/// <summary>
/// Reflux の <c>latest.json</c> (<see cref="RefluxLatestJson"/>) を、要件のタイムスタンプ識別子
/// (<see cref="RecognitionFieldKeys"/>) のマップへ変換する。
///
/// 実機 latest.json のサンプルなしで実装した暫定マッピングのため、各変換は本クラスに集約している。
/// 実出力と差異があった場合は、この変換表のみを調整すれば追従できる。
/// 欠損・未知値のキーは出力に含めない（要件: 検知できなかったキーは省略、展開時は空文字列）。
/// </summary>
public static class RefluxFieldMapper
{
    // diff の頭文字 → 略字 / 正式名。BEGINNER/NORMAL/HYPER/ANOTHER/LEGGENDARIA の頭文字は一意なので
    // 正式名・略字どちらで来ても頭文字で判別できる。
    private static readonly IReadOnlyDictionary<char, (string Short, string Long)> DifficultyByInitial =
        new Dictionary<char, (string, string)>
        {
            ['B'] = ("B", "BEGINNER"),
            ['N'] = ("N", "NORMAL"),
            ['H'] = ("H", "HYPER"),
            ['A'] = ("A", "ANOTHER"),
            ['L'] = ("L", "LEGGENDARIA"),
        };

    // Reflux のランプ表記 → 要件のランプ表記。要件のランプ集合に PFC が無いため FC へ寄せる。
    private static readonly IReadOnlyDictionary<string, string> LampMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["F"] = "FAILED",
            ["FAILED"] = "FAILED",
            ["AC"] = "A-EASY",
            ["A-EASY"] = "A-EASY",
            ["ASSIST"] = "A-EASY",
            ["EC"] = "EASY",
            ["EASY"] = "EASY",
            ["NC"] = "NORMAL",
            ["NORMAL"] = "NORMAL",
            ["HC"] = "HARD",
            ["HARD"] = "HARD",
            ["EX"] = "EX-HARD",
            ["EXH"] = "EX-HARD",
            ["EX-HARD"] = "EX-HARD",
            ["FC"] = "FC",
            ["PFC"] = "FC",
        };

    private static readonly HashSet<string> ValidGrades =
        new(StringComparer.OrdinalIgnoreCase) { "AAA", "AA", "A", "B", "C", "D", "E", "F" };

    public static IReadOnlyDictionary<string, string> Map(RefluxLatestJson json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var fields = new Dictionary<string, string>();

        var title = Clean(json.Title);
        if (title is not null)
            fields[RecognitionFieldKeys.Title] = title;

        if (TryParseNonNegativeInt(json.Level, out var level))
            fields[RecognitionFieldKeys.Level] = level.ToString(CultureInfo.InvariantCulture);

        if (TryResolveDifficulty(json, out var diffShort, out var diffLong))
        {
            fields[RecognitionFieldKeys.DiffLong] = diffLong;
            if (diffShort is not null)
                fields[RecognitionFieldKeys.DiffShort] = diffShort;
        }

        var grade = Clean(json.Grade)?.ToUpperInvariant();
        if (grade is not null && ValidGrades.Contains(grade))
            fields[RecognitionFieldKeys.DjLevel] = grade;

        var lamp = Clean(json.Lamp);
        if (lamp is not null && LampMap.TryGetValue(lamp, out var mappedLamp))
            fields[RecognitionFieldKeys.Lamp] = mappedLamp;

        if (TryParseNonNegativeInt(json.ExScore, out var exScore))
            fields[RecognitionFieldKeys.ExScore] = exScore.ToString(CultureInfo.InvariantCulture);

        // ミスカウント = BAD + POOR（要件「BAD POOR 合計」）。両方が有効値のときのみ算出する。
        if (TryParseNonNegativeInt(json.Bad, out var bad) && TryParseNonNegativeInt(json.Poor, out var poor))
            fields[RecognitionFieldKeys.MissCount] = (bad + poor).ToString(CultureInfo.InvariantCulture);

        return fields;
    }

    private static bool TryResolveDifficulty(RefluxLatestJson json, out string? diffShort, out string diffLong)
    {
        diffShort = null;
        diffLong = string.Empty;

        var rawDiff = Clean(json.Diff);
        if (rawDiff is null) return false;

        // diff に "SPA"/"DPH" のような SP/DP 接頭辞が含まれる場合は剥がす
        var sideFromDiff = ExtractSide(rawDiff, out var diffBody);
        var initial = char.ToUpperInvariant(diffBody.Length > 0 ? diffBody[0] : '\0');
        if (!DifficultyByInitial.TryGetValue(initial, out var entry))
            return false;

        diffLong = entry.Long;

        var side = NormalizeSide(json.PlayType) ?? NormalizeSide(json.Style) ?? sideFromDiff;
        if (side is not null)
            diffShort = side + entry.Short;

        return true;
    }

    /// <summary>"SP"/"DP" を正規化。"1"/"single"→SP、"2"/"double"→DP も許容。</summary>
    private static string? NormalizeSide(string? raw)
    {
        var value = Clean(raw)?.ToUpperInvariant();
        if (value is null) return null;
        if (value is "SP" or "1" or "SINGLE" || value.StartsWith("SP", StringComparison.Ordinal)) return "SP";
        if (value is "DP" or "2" or "DOUBLE" || value.StartsWith("DP", StringComparison.Ordinal)) return "DP";
        return null;
    }

    /// <summary>diff 文字列の先頭が SP/DP なら取り除いて side を返す。</summary>
    private static string? ExtractSide(string diff, out string body)
    {
        var upper = diff.ToUpperInvariant();
        if (upper.StartsWith("SP", StringComparison.Ordinal)) { body = diff[2..]; return "SP"; }
        if (upper.StartsWith("DP", StringComparison.Ordinal)) { body = diff[2..]; return "DP"; }
        body = diff;
        return null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        // Reflux 未取得時の代表値を欠損扱いにする
        if (trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase) || trimmed == "-1")
            return null;
        return trimmed;
    }

    private static bool TryParseNonNegativeInt(string? raw, out int value)
    {
        value = 0;
        var cleaned = Clean(raw);
        if (cleaned is null) return false;
        if (!int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;
        if (parsed < 0) return false;
        value = parsed;
        return true;
    }
}
