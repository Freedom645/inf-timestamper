namespace InfTimestamper.Core.Recognition;

/// <summary>
/// HSV (H/S/V) 全成分の範囲を 1 つのラベルに対応付けるバンド定義。
/// 赤 (H=0) は OpenCV では 0 と 180 をまたぐため、必要に応じて 2 つのバンドに分けて定義する。
/// S/V の範囲は省略時 0-255 全域。色相が同じでも彩度や明度で複数色を区別したいときに利用する。
/// Label は用途別に意味が異なる:
///   - 難易度判定: B / N / H / A / L
///   - ランプ判定: HARD / EX-HARD / EASY / FC / NORMAL / A-EASY (FAILED は HARD と同色)
/// </summary>
public sealed record ColorBand(
    string Label,
    string ColorName,
    int HueMin,
    int HueMax,
    int SatMin = 0,
    int SatMax = 255,
    int ValMin = 0,
    int ValMax = 255)
{
    public bool Matches(int h, int s, int v)
        => h >= HueMin && h <= HueMax
           && s >= SatMin && s <= SatMax
           && v >= ValMin && v <= ValMax;
}

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
        new ColorBand("A", "Red2",  170, 179),
    };
}

public static class DefaultLampColorPalette
{
    /// <summary>
    /// INFINITAS のクリアランプ色バンド既定値。
    /// ランプ文字部分に絞った狭い ROI (49x17 程度) を前提に H/S/V を割り当てる:
    ///   - EASY  (緑):   H=40-87
    ///   - FC    (水色): H=88-100, V≥85 (明るい水色)
    ///   - NORMAL(青):   H=88-100, S≥150 (深い青)
    ///   - A-EASY(紫):   H=95-100 の境界域は S/V で分離、H=101-169 は紫として扱う
    /// HARD と FAILED は同色（赤）のため、Red バンドは HARD のみを返す。
    /// FAILED の区別は次フェーズで別 ROI / 手段で補強する。
    /// </summary>
    public static readonly IReadOnlyList<ColorBand> Bands = new[]
    {
        new ColorBand("HARD",    "Red",      0,  10),
        new ColorBand("EX-HARD", "Yellow",  15,  35),
        new ColorBand("EASY",    "Green",   40,  87),
        new ColorBand("NORMAL",  "Blue",    88, 100, SatMin: 150),
        new ColorBand("FC",      "Cyan",    88, 100, ValMin: 85),
        new ColorBand("A-EASY",  "Purple",  95, 100, SatMin: 100, SatMax: 149, ValMax: 84),
        new ColorBand("A-EASY",  "Magenta",101, 169),
        new ColorBand("HARD",    "Red2",   170, 179),
    };
}
