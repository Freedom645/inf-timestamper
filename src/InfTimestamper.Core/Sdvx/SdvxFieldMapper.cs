using System.Globalization;
using System.Text.Json;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.Sdvx;

/// <summary>
/// SDVX Helper のデータ配信 WebSocket が流すメッセージを、
/// SOUND VOLTEX の識別子（<see cref="GameCatalog.Identifiers"/>）のマップへ変換する。
///
/// マッピング調整はこのクラスだけで完結させる（<c>RefluxFieldMapper</c> / <c>PopnFieldMapper</c> と同じ方針）。
/// 欠損・未知値のキーは出力に含めない（要件: 検知できなかったキーは省略、展開時は空文字列）。
///
/// ペイロードは巨大な base64 画像を含むため、DTO へは起こさず <see cref="JsonElement"/> のまま読む。
/// </summary>
public static class SdvxFieldMapper
{
    /// <summary>スコアの上限。SDVX は 0〜10,000,000。</summary>
    public const int MaxScore = 10_000_000;

    /// <summary>
    /// <c>nowplaying</c> が曲決定画面由来かを判定するときに見る切り出し画像のキー。
    /// ジャケットは選曲画面でも入るので含めない。
    /// </summary>
    private static readonly string[] SongDecidedImageKeys =
    {
        "title", "level", "bpm", "effector", "illustrator",
    };

    // 難易度の略称 → 正式名。SDVX Helper は INF/GRV/HVN/VVD/XCD を MXM 枠に統合して
    // NOV/ADV/EXH/MXM のいずれかで送ってくるが、将来 4th 枠の名前が直接来ても壊さないよう
    // 別名も引けるようにしてある。
    private static readonly IReadOnlyDictionary<string, string> DifficultyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOV"] = "NOVICE",
            ["ADV"] = "ADVANCED",
            ["EXH"] = "EXHAUST",
            ["MXM"] = "MAXIMUM",
            ["INF"] = "INFINITE",
            ["GRV"] = "GRAVITY",
            ["HVN"] = "HEAVENLY",
            ["VVD"] = "VIVID",
            ["XCD"] = "EXCEED",
            ["EXD"] = "EXCEED",
        };

    /// <summary>
    /// クリアランプ。SDVX Helper の <c>clear_lamp</c> enum の値（0〜6）で送られてくる。
    /// 表記は SDVX Helper 側の表示名に合わせている。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> LampNames = new Dictionary<int, string>
    {
        [0] = "NO PLAY",
        [1] = "PLAYED",
        [2] = "COMP",
        [3] = "EXC-COMP",
        [4] = "MAXXIVE",
        [5] = "UC",
        [6] = "PUC",
    };

    /// <summary>グレード。スコアから一意に決まる閉じた集合なので、未知値は捨てる。</summary>
    private static readonly HashSet<string> ValidGrades = new(StringComparer.OrdinalIgnoreCase)
    {
        "S", "AAA+", "AAA", "AA+", "AA", "A+", "A", "B", "C", "D",
    };

    /// <summary>
    /// <c>nowplaying</c> メッセージが「曲決定画面」由来か（＝プレイ開始とみなせるか）を判定する。
    ///
    /// SDVX Helper は <c>nowplaying</c> を 2 箇所から流す。選曲画面でカーソルが動いたときと、
    /// 曲決定画面（楽曲情報画面）を読めたとき。前者は切り出し画像がジャケットしか無く、
    /// 後者はタイトル / レベル / BPM / エフェクター / イラストレーターの画像も入る。
    /// この差でプレイ開始だけを拾う。
    /// </summary>
    public static bool IsSongDecided(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return false;
        if (!data.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var key in SongDecidedImageKeys)
        {
            if (images.TryGetProperty(key, out var image)
                && image.ValueKind == JsonValueKind.String
                && !image.ValueEquals(string.Empty))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// <c>nowplaying</c> メッセージの曲情報を識別子へ変換する。成績はまだ出ていないので曲情報だけ。
    /// </summary>
    public static IReadOnlyDictionary<string, string> MapNowPlaying(JsonElement data)
    {
        var fields = new Dictionary<string, string>();
        if (data.ValueKind != JsonValueKind.Object) return fields;

        PutTitle(fields, data, "title");
        PutDifficulty(fields, data, "difficulty");
        PutLevel(fields, data, "level");
        return fields;
    }

    /// <summary>
    /// <c>today_results</c> メッセージの 1 エントリ（このプレイのリザルト）を識別子へ変換する。
    /// </summary>
    public static IReadOnlyDictionary<string, string> MapResult(JsonElement item)
    {
        var fields = new Dictionary<string, string>();
        if (item.ValueKind != JsonValueKind.Object) return fields;

        PutTitle(fields, item, "title");
        PutDifficulty(fields, item, "difficulty");
        PutLevel(fields, item, "lv");

        if (TryGetInt(item, "score", out var score) && score > 0 && score <= MaxScore)
        {
            fields[FieldKeys.Score] = score.ToString(CultureInfo.InvariantCulture);
            fields[FieldKeys.ScoreShort] = (score / 1000).ToString(CultureInfo.InvariantCulture);
        }

        // EX スコアはリザルト画面が EX スコア表示のときだけ読める。読めなければ 0 で来る
        if (TryGetInt(item, "exscore", out var exScore) && exScore > 0)
            fields[FieldKeys.ExScore] = exScore.ToString(CultureInfo.InvariantCulture);

        var grade = Clean(GetString(item, "grade"));
        if (grade is not null && ValidGrades.Contains(grade))
            fields[FieldKeys.Grade] = grade.ToUpperInvariant();

        var lamp = ReadLampName(item);
        if (lamp is not null)
            fields[FieldKeys.ClearLamp] = lamp;

        return fields;
    }

    /// <summary>
    /// <c>today_results</c> のエントリが記録された時刻（<c>timestamp</c>、Unix 秒）を解釈する。
    /// 前のプレイのエントリを取り違えない判定に使う。
    /// </summary>
    public static bool TryGetResultTime(JsonElement item, out DateTimeOffset value)
    {
        value = default;
        if (!TryGetLong(item, "timestamp", out var seconds) || seconds <= 0) return false;
        try
        {
            value = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void PutTitle(IDictionary<string, string> fields, JsonElement source, string property)
    {
        var title = Clean(GetString(source, property));
        if (title is not null)
            fields[FieldKeys.Title] = title;
    }

    private static void PutDifficulty(IDictionary<string, string> fields, JsonElement source, string property)
    {
        var diff = Clean(GetString(source, property));
        if (diff is null) return;

        fields[FieldKeys.DiffShort] = diff.ToUpperInvariant();
        fields[FieldKeys.DiffLong] = DifficultyNames.TryGetValue(diff, out var longName)
            ? longName
            : diff.ToUpperInvariant();
    }

    private static void PutLevel(IDictionary<string, string> fields, JsonElement source, string property)
    {
        if (!source.TryGetProperty(property, out var element)) return;

        int level;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetInt32(out var number):
                level = number;
                break;
            case JsonValueKind.String when int.TryParse(
                element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                level = parsed;
                break;
            default:
                return;
        }

        if (level > 0)
            fields[FieldKeys.Level] = level.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>クリアランプは enum 値（数値）で来るが、将来文字列化されても拾えるようにしてある。</summary>
    private static string? ReadLampName(JsonElement item)
    {
        if (!item.TryGetProperty("lamp", out var element)) return null;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
            return LampNames.TryGetValue(value, out var name) ? name : null;

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = Clean(element.GetString());
            if (raw is null) return null;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                return LampNames.TryGetValue(numeric, out var name) ? name : null;
            return raw.ToUpperInvariant();
        }

        return null;
    }

    private static string? GetString(JsonElement source, string property)
        => source.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static bool TryGetInt(JsonElement source, string property, out int value)
    {
        value = 0;
        return source.TryGetProperty(property, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt32(out value);
    }

    private static bool TryGetLong(JsonElement source, string property, out long value)
    {
        value = 0;
        return source.TryGetProperty(property, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt64(out value);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
