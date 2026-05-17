namespace InfTimestamper.Core.Recognition;

/// <summary>
/// HSV 色相 [HueMin, HueMax] を 1 つのラベルに対応付けるバンド定義。
/// 赤 (H=0) は OpenCV では 0 と 180 をまたぐため、必要に応じて 2 つのバンドに分けて定義する。
/// Label は用途別に意味が異なる:
///   - 難易度判定: B / N / H / A / L
///   - ランプ判定: HARD / EX-HARD / EASY / FC / NORMAL / A-EASY (FAILED は HARD と同色)
/// </summary>
public sealed record ColorBand(string Label, string ColorName, int HueMin, int HueMax);

public static class DefaultDifficultyColorPalette
{
    /// <summary>
    /// INFINITAS の難易度色バンド既定値（OpenCV HSV: H=0-179）。
    /// B (BEGINNER): 緑 / N (NORMAL): 青 / H (HYPER): 黄 / A (ANOTHER): 赤 / L (LEGGENDARIA): 紫
    /// </summary>
    public static readonly IReadOnlyList<ColorBand> Bands = new[]
    {
        new ColorBand("A", "Red",     0,  10),
        new ColorBand("H", "Yellow", 20,  35),
        new ColorBand("B", "Green",  40,  85),
        new ColorBand("N", "Blue",   90, 135),
        new ColorBand("L", "Purple",140, 165),
        new ColorBand("A", "Red2",  170, 179),  // H 折り返し側
    };
}

public static class DefaultLampColorPalette
{
    /// <summary>
    /// INFINITAS のクリアランプ色バンド既定値。
    /// A-EASY: 紫 / EASY: 緑 / NORMAL: 青 / HARD: 赤 (FAILED と同色) / EX-HARD: 黄 / FC: 水色
    /// HARD と FAILED は同色のため色判定では区別できない（次フェーズで別 ROI/手段で補強）。
    /// 暫定的に Red バンドは HARD を返す。
    /// </summary>
    public static readonly IReadOnlyList<ColorBand> Bands = new[]
    {
        new ColorBand("HARD",    "Red",      0,  10),
        new ColorBand("EX-HARD", "Yellow",  20,  35),
        new ColorBand("EASY",    "Green",   40,  78),
        new ColorBand("FC",      "Cyan",    80,  95),
        new ColorBand("NORMAL",  "Blue",   100, 135),
        new ColorBand("A-EASY",  "Purple", 140, 165),
        new ColorBand("HARD",    "Red2",   170, 179),  // H 折り返し側
    };
}
