using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Reflux;

/// <summary>
/// Reflux が出力する <c>latest.json</c> のうち、本アプリが利用するフィールドの DTO。
/// Reflux は全フィールドを文字列として書き出すため、ここでも文字列で受けて
/// <see cref="RefluxFieldMapper"/> で各識別子へ変換する。
/// 前段 Python 実装 (reflux_file_watcher.py の LatestJson TypedDict) に対応。
/// </summary>
public sealed class RefluxLatestJson
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }

    /// <summary>譜面の難易度。NORMAL/HYPER/ANOTHER 等の名称、または B/N/H/A/L の略字を想定。</summary>
    [JsonPropertyName("diff")]
    public string? Diff { get; set; }

    /// <summary>
    /// **プレイヤーサイド**（実出力は <c>"P1"</c> / <c>"P2"</c>）。SP/DP 区分ではない。
    /// SP/DP は <see cref="Diff"/> の接頭辞（<c>"SPN"</c> 等）から取る。
    /// </summary>
    [JsonPropertyName("playtype")]
    public string? PlayType { get; set; }

    /// <summary>
    /// **プレイオプション**（実出力は <c>"MIRROR"</c> / <c>"OFF"</c> 等）。SP/DP 区分ではない。
    /// </summary>
    [JsonPropertyName("style")]
    public string? Style { get; set; }

    /// <summary>DJ レベル。AAA/AA/A/B/C/D/E/F を想定。</summary>
    [JsonPropertyName("grade")]
    public string? Grade { get; set; }

    /// <summary>クリアランプ。NP/F/AC/EC/NC/HC/EX/FC/PFC を想定。</summary>
    [JsonPropertyName("lamp")]
    public string? Lamp { get; set; }

    [JsonPropertyName("exscore")]
    public string? ExScore { get; set; }

    [JsonPropertyName("bad")]
    public string? Bad { get; set; }

    [JsonPropertyName("poor")]
    public string? Poor { get; set; }
}
