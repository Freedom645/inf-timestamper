using System.Globalization;
using System.Text.Json;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.DakenCounter;

/// <summary>
/// 打鍵カウンタ（inf_daken_counter_obsw）のデータ配信 WebSocket が流すメッセージを、
/// INFINITAS の識別子（<see cref="GameCatalog.Identifiers"/>）のマップへ変換する。
/// 識別子の集合は Reflux（<c>RefluxFieldMapper</c>）と同じで、表記も Reflux 側に揃えてある。
///
/// マッピング調整はこのクラスだけで完結させる（他のゲームの FieldMapper と同じ方針）。
/// 欠損・未知値のキーは出力に含めない（要件: 検知できなかったキーは省略、展開時は空文字列）。
/// 実機サンプル（docs/sample/daken-counter/）で Reflux の記録と突き合わせ済み。
///
/// <c>pre_score</c> / <c>pre_bp</c> / <c>pre_lamp</c> と <c>best_*</c> は自己ベスト側の値で
/// このプレイの成績ではないため取り込まない（pop'n / SDVX と同じ方針）。
/// </summary>
public static class DakenCounterFieldMapper
{
    // 難易度の頭文字 → 正式名。打鍵カウンタは "SPA" / "DPH" / "DBA"（DOUBLE BATTLE）の形で送ってくる
    private static readonly IReadOnlyDictionary<char, string> DifficultyByInitial = new Dictionary<char, string>
    {
        ['B'] = "BEGINNER",
        ['N'] = "NORMAL",
        ['H'] = "HYPER",
        ['A'] = "ANOTHER",
        ['L'] = "LEGGENDARIA",
    };

    /// <summary>
    /// クリアランプ。打鍵カウンタの <c>clear_lamp</c> enum の値（0〜7）で送られてくる。
    /// 表記は Reflux 側（<c>RefluxFieldMapper</c>）に揃える。0（NO PLAY）は欠損扱い。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> LampNames = new Dictionary<int, string>
    {
        [1] = "FAILED",
        [2] = "A-EASY",
        [3] = "EASY",
        [4] = "NORMAL",
        [5] = "HARD",
        [6] = "EX-HARD",
        [7] = "FC",
    };

    /// <summary>
    /// DJ レベルの下限（理論値に対する割合を 9 分割したもの）。打鍵カウンタは DJ レベルを送ってこないので、
    /// EX スコアとノーツ数から求める。上から順に見る。
    /// </summary>
    private static readonly (int Ninths, string Grade)[] DjLevelThresholds =
    {
        (8, "AAA"), (7, "AA"), (6, "A"), (5, "B"), (4, "C"), (3, "D"), (2, "E"),
    };

    /// <summary>
    /// <c>history_cursong</c>（選曲画面・リザルト画面で見えている譜面）から曲情報を取り出す。
    /// 曲名は <c>music</c> に入っている（<c>today_updates</c> の <c>title</c> とキー名が違う）。
    /// </summary>
    public static IReadOnlyDictionary<string, string> MapCurrentSong(JsonElement data)
    {
        var fields = new Dictionary<string, string>();
        if (data.ValueKind != JsonValueKind.Object) return fields;

        AddText(fields, FieldKeys.Title, data, "music");
        AddSongInfo(fields, data);
        return fields;
    }

    /// <summary><c>today_updates</c> の 1 エントリ（このプレイのリザルト）を変換する。</summary>
    public static IReadOnlyDictionary<string, string> MapResult(JsonElement item)
    {
        var fields = new Dictionary<string, string>();
        if (item.ValueKind != JsonValueKind.Object) return fields;

        AddText(fields, FieldKeys.Title, item, "title");
        AddSongInfo(fields, item);

        var score = ReadNonNegativeInt(item, "score");
        if (score is not null)
            fields[FieldKeys.ExScore] = score.Value.ToString(CultureInfo.InvariantCulture);

        // bp は BAD + POOR。途中落ち・途中終了（dead）のプレイでは、打鍵カウンタが落ちた後の残りノーツを
        // POOR に足し込むため 700 や 1000 といった実態の無い値になる（ゲームのリザルト画面でも BP は "-" 表示）。
        // そのため dead のときは欠損扱いにする。完走して FAILED になったプレイの BP は正しいので記録する
        var bp = ReadNonNegativeInt(item, "bp");
        if (bp is not null && !IsDead(item))
            fields[FieldKeys.MissCount] = bp.Value.ToString(CultureInfo.InvariantCulture);

        var lamp = ReadNonNegativeInt(item, "lamp");
        if (lamp is not null && LampNames.TryGetValue(lamp.Value, out var lampName))
            fields[FieldKeys.Lamp] = lampName;

        var notes = ReadNonNegativeInt(item, "notes");
        if (score is not null && notes is > 0)
        {
            var grade = ResolveDjLevel(score.Value, notes.Value);
            if (grade is not null)
                fields[FieldKeys.DjLevel] = grade;
        }

        return fields;
    }

    /// <summary>
    /// EX スコアとノーツ数から DJ レベルを求める（理論値 = ノーツ数 × 2）。
    /// 割合の比較は浮動小数の丸めで境界がずれないよう整数で行う。
    /// </summary>
    public static string? ResolveDjLevel(int exScore, int notes)
    {
        if (notes <= 0 || exScore < 0) return null;

        long scaled = (long)exScore * 9;
        long max = (long)notes * 2;
        foreach (var (ninths, grade) in DjLevelThresholds)
        {
            if (scaled >= max * ninths) return grade;
        }
        return "F";
    }

    /// <summary>
    /// <c>today_updates</c> の <c>timestamp</c>（UNIX 秒。リザルト画面に入った時刻）を読む。
    /// </summary>
    public static bool TryGetResultTime(JsonElement item, out DateTimeOffset time)
    {
        time = default;
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("timestamp", out var raw)
            || raw.ValueKind != JsonValueKind.Number
            || !raw.TryGetInt64(out var seconds)
            || seconds <= 0)
        {
            return false;
        }

        time = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime();
        return true;
    }

    /// <summary>レベルと難易度。<c>history_cursong</c> と <c>today_updates</c> で同じキー名。</summary>
    private static void AddSongInfo(Dictionary<string, string> fields, JsonElement element)
    {
        var level = ReadNonNegativeInt(element, "lv");
        if (level is > 0)
            fields[FieldKeys.Level] = level.Value.ToString(CultureInfo.InvariantCulture);

        var difficulty = ReadText(element, "difficulty")?.ToUpperInvariant();
        if (difficulty is { Length: 3 }
            && (difficulty.StartsWith("SP", StringComparison.Ordinal)
                || difficulty.StartsWith("DP", StringComparison.Ordinal)
                || difficulty.StartsWith("DB", StringComparison.Ordinal))
            && DifficultyByInitial.TryGetValue(difficulty[2], out var diffLong))
        {
            fields[FieldKeys.DiffShort] = difficulty;
            fields[FieldKeys.DiffLong] = diffLong;
        }
    }

    /// <summary>途中落ち・途中終了したプレイか（<c>dead</c>）。</summary>
    private static bool IsDead(JsonElement element)
        => element.TryGetProperty("dead", out var dead) && dead.ValueKind == JsonValueKind.True;

    private static void AddText(Dictionary<string, string> fields, string key, JsonElement element, string name)
    {
        var value = ReadText(element, name);
        if (value is not null)
            fields[key] = value;
    }

    private static string? ReadText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>数値でも数字の文字列でも受ける（<c>lv</c> は文字列、<c>notes</c> は未取得時に空文字列で来る）。</summary>
    private static int? ReadNonNegativeInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;

        int parsed;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetInt32(out parsed):
                break;
            case JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed):
                break;
            default:
                return null;
        }

        return parsed >= 0 ? parsed : null;
    }
}
