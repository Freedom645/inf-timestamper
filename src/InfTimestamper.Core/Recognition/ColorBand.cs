namespace InfTimestamper.Core.Recognition;

/// <summary>
/// HSV 色相 [HueMin, HueMax] を 1 つの難易度ラベル（B/N/H/A/L）に対応付けるバンド定義。
/// 赤 (H=0) は OpenCV では 0 と 180 をまたぐため、必要に応じて 2 つのバンドに分けて定義する。
/// </summary>
public sealed record ColorBand(string DifficultyShort, string ColorName, int HueMin, int HueMax);

public static class DefaultDifficultyColorPalette
{
    /// <summary>
    /// INFINITAS の難易度色バンド既定値（OpenCV HSV: H=0-179）。
    /// 実機ログから調整可能。
    /// </summary>
    public static readonly IReadOnlyList<ColorBand> Bands = new[]
    {
        new ColorBand("A", "Red",     0,  10),  // ANOTHER (赤)
        new ColorBand("H", "Yellow", 20,  35),  // HYPER (黄)
        new ColorBand("B", "Green",  40,  85),  // BEGINNER (緑)
        new ColorBand("N", "Blue",   90, 135),  // NORMAL (青)
        new ColorBand("L", "Purple",140, 165),  // LEGGENDARIA (紫)
        new ColorBand("A", "Red2",  170, 179),  // ANOTHER (赤、H 折り返し側)
    };
}
