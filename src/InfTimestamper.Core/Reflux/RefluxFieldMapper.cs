using System.Globalization;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.Reflux;

/// <summary>
/// Reflux の <c>latest.json</c> (<see cref="RefluxLatestJson"/>) を、要件のタイムスタンプ識別子
/// (<see cref="FieldKeys"/>) のマップへ変換する。
///
/// 各変換は本クラスに集約しているため、実出力と差異があった場合はこの変換表のみを調整すれば追従できる。
/// 実機サンプル（docs/sample/reflux/latest.json）で全フィールドの展開を確認済み。
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
            fields[FieldKeys.Title] = title;

        if (TryParseNonNegativeInt(json.Level, out var level))
            fields[FieldKeys.Level] = level.ToString(CultureInfo.InvariantCulture);

        if (TryResolveDifficulty(json, out var diffShort, out var diffLong))
        {
            fields[FieldKeys.DiffLong] = diffLong;
            if (diffShort is not null)
                fields[FieldKeys.DiffShort] = diffShort;
        }

        var grade = Clean(json.Grade)?.ToUpperInvariant();
        if (grade is not null && ValidGrades.Contains(grade))
            fields[FieldKeys.DjLevel] = grade;

        var lamp = Clean(json.Lamp);
        if (lamp is not null && LampMap.TryGetValue(lamp, out var mappedLamp))
            fields[FieldKeys.Lamp] = mappedLamp;

        if (TryParseNonNegativeInt(json.ExScore, out var exScore))
            fields[FieldKeys.ExScore] = exScore.ToString(CultureInfo.InvariantCulture);

        // ミスカウント = BAD + POOR（要件「BAD POOR 合計」）。両方が有効値のときのみ算出する。
        if (TryParseNonNegativeInt(json.Bad, out var bad) && TryParseNonNegativeInt(json.Poor, out var poor))
            fields[FieldKeys.MissCount] = (bad + poor).ToString(CultureInfo.InvariantCulture);

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

        // SP/DP の正本は diff の接頭辞（実出力は "SPN" のような形）。
        // playtype はプレイヤーサイド (P1/P2)、style はオプション (MIRROR 等) で SP/DP ではないため、
        // diff に接頭辞が無かった場合に限り、明示的な "SP"/"DP" 表記だけを保険として拾う。
        var side = sideFromDiff ?? NormalizeSide(json.PlayType) ?? NormalizeSide(json.Style);
        if (side is not null)
            diffShort = side + entry.Short;

        return true;
    }

    /// <summary>
    /// 明示的な "SP"/"DP" 表記のみを正規化する。
    /// "1"/"2" のような数値表現は、実出力ではプレイヤーサイド (P1/P2) を指すため受け付けない。
    /// </summary>
    private static string? NormalizeSide(string? raw)
    {
        var value = Clean(raw)?.ToUpperInvariant();
        if (value is null) return null;
        if (value.StartsWith("SP", StringComparison.Ordinal)) return "SP";
        if (value.StartsWith("DP", StringComparison.Ordinal)) return "DP";
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
