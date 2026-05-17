using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

/// <summary>
/// 指定の色バンドパレットを使い、ROI 内の HSV 支配色から該当ラベルを判定する。
/// 難易度（B/N/H/A/L）・ランプ（HARD/EX-HARD/EASY/FC/NORMAL/A-EASY）の両用途で再利用可能。
/// </summary>
public sealed class ColorBandDetector
{
    public const int DefaultSaturationThreshold = 80;
    public const int DefaultValueThreshold = 80;
    public const double DefaultMinDominantRatio = 0.20;

    private readonly IReadOnlyList<ColorBand> _bands;
    private readonly int _saturationThreshold;
    private readonly int _valueThreshold;
    private readonly double _minDominantRatio;

    public ColorBandDetector(
        IReadOnlyList<ColorBand> bands,
        int saturationThreshold = DefaultSaturationThreshold,
        int valueThreshold = DefaultValueThreshold,
        double minDominantRatio = DefaultMinDominantRatio)
    {
        _bands = bands ?? throw new ArgumentNullException(nameof(bands));
        _saturationThreshold = saturationThreshold;
        _valueThreshold = valueThreshold;
        _minDominantRatio = minDominantRatio;
    }

    public static ColorBandDetector ForDifficulty()
        => new(DefaultDifficultyColorPalette.Bands);

    public static ColorBandDetector ForLamp()
        => new(DefaultLampColorPalette.Bands);

    /// <summary>
    /// 与えられた ROI の HSV 分布から、最も支配的な色バンドに対応するラベルを返す。
    /// 高彩度・高明度ピクセル中で支配率が <see cref="DefaultMinDominantRatio"/> 未満なら null。
    /// </summary>
    public string? Detect(Mat roi)
    {
        if (roi is null || roi.Empty()) return null;
        return ComputeBandStats(roi).DominantLabel;
    }

    public ColorDetectionStats ComputeBandStats(Mat roi)
    {
        if (roi is null || roi.Empty())
            return new ColorDetectionStats(null, 0.0, new Dictionary<string, int>());

        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

        var counts = new Dictionary<string, int>();
        int totalSaturated = 0;

        for (int y = 0; y < hsv.Rows; y++)
        {
            for (int x = 0; x < hsv.Cols; x++)
            {
                var p = hsv.At<Vec3b>(y, x);
                int h = p.Item0;
                int s = p.Item1;
                int v = p.Item2;
                if (s < _saturationThreshold || v < _valueThreshold) continue;

                totalSaturated++;
                foreach (var band in _bands)
                {
                    if (h >= band.HueMin && h <= band.HueMax)
                    {
                        counts.TryGetValue(band.Label, out var c);
                        counts[band.Label] = c + 1;
                        break;
                    }
                }
            }
        }

        if (totalSaturated == 0 || counts.Count == 0)
            return new ColorDetectionStats(null, 0.0, counts);

        var best = counts.OrderByDescending(kv => kv.Value).First();
        var ratio = (double)best.Value / totalSaturated;
        var result = ratio >= _minDominantRatio ? best.Key : null;
        return new ColorDetectionStats(result, ratio, counts);
    }
}

public sealed record ColorDetectionStats(
    string? DominantLabel,
    double DominantRatio,
    IReadOnlyDictionary<string, int> BandCounts);
