using System.Globalization;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.Popn;

/// <summary>
/// popn-lively-tracker の <c>result.json</c>（<see cref="PopnResultJson"/>）を、
/// pop'n music の識別子（<see cref="GameCatalog.Identifiers"/>）のマップへ変換する。
///
/// マッピング調整はこのクラスだけで完結させる（INFINITAS 側の <c>RefluxFieldMapper</c> と同じ方針）。
/// 欠損・未知値のキーは出力に含めない（要件: 検知できなかったキーは省略、展開時は空文字列）。
/// </summary>
public static class PopnFieldMapper
{
    /// <summary>スコアの上限。仕様上 0〜100000。</summary>
    public const int MaxScore = 100000;

    // 譜面種別 → (略式表記, 正式名)。EX は 1 文字に潰すと EASY と衝突するため 2 文字のまま。
    private static readonly IReadOnlyDictionary<string, (string Short, string Long)> SheetMap =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["EASY"] = ("E", "EASY"),
            ["NORMAL"] = ("N", "NORMAL"),
            ["HYPER"] = ("H", "HYPER"),
            ["EX"] = ("EX", "EX"),
        };

    // クリアランクは閉じた集合なので厳密に検証する（file-output.md「クリアランクの閾値」）。
    private static readonly HashSet<string> ValidRanks =
        new(StringComparer.OrdinalIgnoreCase) { "S", "AAA", "AA", "A", "B", "C", "D", "E" };

    /// <summary>
    /// メダル名は画面のアイコン名そのままで、過去に改名された実績がある（2026-08-30 の改名）。
    /// 集合で弾くと将来の追加・改名で取りこぼすため、検証には使わず参考情報として持つ。
    /// </summary>
    public static IReadOnlyList<string> KnownMedalNames { get; } = new[]
    {
        "青丸", "青菱", "青星", "若葉", "銅丸", "銅菱", "銅星", "銀丸", "銀菱", "銀星", "金星",
    };

    public static IReadOnlyDictionary<string, string> Map(PopnResultJson json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var fields = new Dictionary<string, string>();

        // 譜面を特定できなかったプレイは music が null。成績側（rank/medal/score/bad）は
        // このプレイの実測値として有効なので、曲情報だけを欠損扱いにする。
        var music = json.Music;
        if (music is not null)
        {
            var title = Clean(music.Title);
            if (title is not null)
                fields[FieldKeys.Title] = title;

            if (music.Level is int level && level > 0)
                fields[FieldKeys.Level] = level.ToString(CultureInfo.InvariantCulture);

            var sheet = Clean(music.Sheet);
            if (sheet is not null && SheetMap.TryGetValue(sheet, out var mappedSheet))
            {
                fields[FieldKeys.DiffLong] = mappedSheet.Long;
                fields[FieldKeys.DiffShort] = mappedSheet.Short;
            }
        }

        var rank = Clean(json.RankName)?.ToUpperInvariant();
        if (rank is not null && ValidRanks.Contains(rank))
            fields[FieldKeys.Rank] = rank;

        var medal = Clean(json.MedalName);
        if (medal is not null)
            fields[FieldKeys.Medal] = medal;

        if (json.Score is int score && score >= 0 && score <= MaxScore)
            fields[FieldKeys.Score] = score.ToString(CultureInfo.InvariantCulture);

        if (json.Judge?.Bad is int bad && bad >= 0)
            fields[FieldKeys.Bad] = bad.ToString(CultureInfo.InvariantCulture);

        return fields;
    }

    /// <summary>
    /// <c>result.json</c> の <c>time</c>（<c>yyyy-MM-dd HH:mm:ss</c>、ローカル時刻）を解釈する。
    /// リザルトは画面表示から数秒遅れて書かれるため、直前のプレイのものを取り違えない判定に使う。
    /// </summary>
    public static bool TryParseTime(string? raw, out DateTimeOffset value)
    {
        value = default;
        var cleaned = Clean(raw);
        if (cleaned is null) return false;
        if (!DateTime.TryParseExact(cleaned, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
        return true;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
