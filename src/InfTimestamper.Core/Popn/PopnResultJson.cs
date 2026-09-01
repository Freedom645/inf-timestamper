using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Popn;

/// <summary>
/// popn-lively-tracker が出力する <c>result.json</c> のうち、本アプリが利用するフィールドの DTO。
/// 仕様は popn-lively-tracker の <c>docs/file-output.md</c>（互換契約としてキー名が固定されている）。
/// Reflux と違い値は素の JSON 型で書かれるため、数値・真偽値はそのままの型で受ける。
///
/// 注意: <c>record</c> セクション（<c>clear_rank</c> / <c>medal</c> 等）は**自己ベスト**であって
/// このプレイの成績ではないため、意図的に取り込んでいない。
/// このプレイの成績は <see cref="RankName"/> / <see cref="MedalName"/> / <see cref="Score"/> 側。
/// </summary>
public sealed class PopnResultJson
{
    /// <summary>リザルトを検知した日時（<c>yyyy-MM-dd HH:mm:ss</c>、ローカル時刻）。</summary>
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    /// <summary>
    /// 譜面の特定方法。<c>特定できず</c> のとき <see cref="Music"/> は null になる。
    /// </summary>
    [JsonPropertyName("identified_by")]
    public string? IdentifiedBy { get; set; }

    /// <summary>このプレイのスコア（0〜100000）。</summary>
    [JsonPropertyName("score")]
    public int? Score { get; set; }

    /// <summary>このプレイのクリアランク名（S / AAA / AA / A / B / C / D / E）。</summary>
    [JsonPropertyName("rank_name")]
    public string? RankName { get; set; }

    /// <summary>このプレイのクリアメダル名（青丸〜金星）。</summary>
    [JsonPropertyName("medal_name")]
    public string? MedalName { get; set; }

    /// <summary>判定内訳。BAD のみコンボが切れる。</summary>
    [JsonPropertyName("judge")]
    public PopnJudgeJson? Judge { get; set; }

    /// <summary>曲情報。譜面を特定できなかった場合は null。</summary>
    [JsonPropertyName("music")]
    public PopnMusicJson? Music { get; set; }
}

public sealed class PopnJudgeJson
{
    [JsonPropertyName("cool")]
    public int? Cool { get; set; }

    [JsonPropertyName("great")]
    public int? Great { get; set; }

    [JsonPropertyName("good")]
    public int? Good { get; set; }

    [JsonPropertyName("bad")]
    public int? Bad { get; set; }
}

public sealed class PopnMusicJson
{
    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    /// <summary>譜面種別。EASY / NORMAL / HYPER / EX。</summary>
    [JsonPropertyName("sheet")]
    public string? Sheet { get; set; }

    /// <summary>譜面のレベル（1〜50）。</summary>
    [JsonPropertyName("level")]
    public int? Level { get; set; }
}
