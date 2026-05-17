using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public sealed class ColorDifficultyDetector
{
    public const int DefaultSaturationThreshold = 80;
    public const int DefaultValueThreshold = 80;
    public const double DefaultMinDominantRatio = 0.20;

    private readonly IReadOnlyList<ColorBand> _bands;
    private readonly int _saturationThreshold;
    private readonly int _valueThreshold;
    private readonly double _minDominantRatio;

    public ColorDifficultyDetector()
        : this(DefaultDifficultyColorPalette.Bands) { }

    public ColorDifficultyDetector(
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

    /// <summary>
    /// 与えられた ROI の HSV 分布から、最も支配的な色バンドに対応する難易度短縮値（B/N/H/A/L）を返す。
    /// 高彩度・高明度ピクセル中で支配率が <see cref="DefaultMinDominantRatio"/> 未満なら null。
    /// </summary>
    public string? DetectDifficulty(Mat roi)
    {
        if (roi is null || roi.Empty()) return null;
        var stats = ComputeBandStats(roi);
        return stats.DominantDifficulty;
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
                        counts.TryGetValue(band.DifficultyShort, out var c);
                        counts[band.DifficultyShort] = c + 1;
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
    string? DominantDifficulty,
    double DominantRatio,
    IReadOnlyDictionary<string, int> BandCounts);
