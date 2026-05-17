using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class ColorBandDetectorTests
{
    private static Mat MakeSolid(int width, int height, byte b, byte g, byte r)
        => new(height, width, MatType.CV_8UC3, new Scalar(b, g, r));

    // HSV (OpenCV: H=0-179) を指定して BGR Mat を作る。実画像の色傾向を再現するため。
    private static Mat MakeHsvSolid(int width, int height, int h, int s, int v)
    {
        using var hsv = new Mat(height, width, MatType.CV_8UC3, new Scalar(h, s, v));
        var bgr = new Mat();
        Cv2.CvtColor(hsv, bgr, ColorConversionCodes.HSV2BGR);
        return bgr;
    }

    [Theory]
    [InlineData(0, 0, 255, "A")]
    [InlineData(0, 255, 255, "H")]
    [InlineData(0, 255, 0, "B")]
    [InlineData(255, 0, 0, "N")]
    [InlineData(255, 0, 255, "L")]
    public void Detect_DifficultyPalette_ReturnsExpectedLabel(byte b, byte g, byte r, string expected)
    {
        using var mat = MakeSolid(64, 32, b, g, r);
        var detector = ColorBandDetector.ForDifficulty();
        Assert.Equal(expected, detector.Detect(mat));
    }

    // ランプは実 INFINITAS の代表 HSV に近い値で検証する。
    // pure RGB は HSV 位置が実色と乖離する (例: pure blue は H=120 で紫寄り) ため
    // ランプテストでは HSV 直指定を使う。
    [Theory]
    [InlineData(0, 200, 200, "HARD")]    // 赤 (FAILED も同色)
    [InlineData(25, 200, 200, "EX-HARD")] // 黄
    [InlineData(60, 200, 200, "EASY")]   // 緑
    [InlineData(95, 80, 209, "FC")]      // 水色: 低彩度・高明度 (実画像 mean HSV ≒ 93,78,209)
    [InlineData(95, 200, 200, "NORMAL")] // 青: 高彩度
    [InlineData(130, 120, 200, "A-EASY")] // 紫 (実画像 mean HSV ≒ 129,112,217)
    public void Detect_LampPalette_ReturnsExpectedLabel(int h, int s, int v, string expected)
    {
        using var mat = MakeHsvSolid(64, 32, h, s, v);
        var detector = ColorBandDetector.ForLamp();
        Assert.Equal(expected, detector.Detect(mat));
    }

    [Fact]
    public void Detect_BlackImage_ReturnsNull()
    {
        using var mat = MakeSolid(64, 32, 0, 0, 0);
        Assert.Null(ColorBandDetector.ForDifficulty().Detect(mat));
        Assert.Null(ColorBandDetector.ForLamp().Detect(mat));
    }

    [Fact]
    public void Detect_WhiteImage_ReturnsNull()
    {
        // 白は S=0 で全ピクセルが彩度しきい値未満
        using var mat = MakeSolid(64, 32, 255, 255, 255);
        Assert.Null(ColorBandDetector.ForDifficulty().Detect(mat));
        Assert.Null(ColorBandDetector.ForLamp().Detect(mat));
    }

    [Fact]
    public void Detect_EmptyMat_ReturnsNull()
    {
        Assert.Null(ColorBandDetector.ForDifficulty().Detect(new Mat()));
    }

    [Fact]
    public void ComputeBandStats_PureBlue_ReportsHighRatio()
    {
        using var mat = MakeSolid(64, 32, 255, 0, 0);
        var stats = ColorBandDetector.ForDifficulty().ComputeBandStats(mat);

        Assert.Equal("N", stats.DominantLabel);
        Assert.True(stats.DominantRatio > 0.95, $"期待: >0.95、実測: {stats.DominantRatio}");
    }

    [Fact]
    public void Detect_BelowMinDominantRatio_ReturnsNull()
    {
        using var mat = MakeSolid(64, 32, 5, 5, 5);
        var detector = new ColorBandDetector(
            DefaultDifficultyColorPalette.Bands,
            minDominantRatio: 0.5);
        Assert.Null(detector.Detect(mat));
    }
}
