using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class ColorBandDetectorTests
{
    private static Mat MakeSolid(int width, int height, byte b, byte g, byte r)
        => new(height, width, MatType.CV_8UC3, new Scalar(b, g, r));

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

    [Theory]
    [InlineData(0, 0, 255, "HARD")]      // 赤
    [InlineData(0, 255, 255, "EX-HARD")] // 黄
    [InlineData(0, 255, 0, "EASY")]      // 緑
    [InlineData(255, 255, 0, "FC")]      // 水色 (Cyan: B=255, G=255, R=0)
    [InlineData(255, 0, 0, "NORMAL")]    // 青
    [InlineData(255, 0, 255, "A-EASY")]  // 紫 (Magenta)
    public void Detect_LampPalette_ReturnsExpectedLabel(byte b, byte g, byte r, string expected)
    {
        using var mat = MakeSolid(64, 32, b, g, r);
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
