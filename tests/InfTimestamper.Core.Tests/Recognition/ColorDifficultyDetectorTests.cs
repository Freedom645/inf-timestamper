using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class ColorDifficultyDetectorTests
{
    private static Mat MakeSolid(int width, int height, byte b, byte g, byte r)
        => new(height, width, MatType.CV_8UC3, new Scalar(b, g, r));

    [Theory]
    [InlineData(0, 0, 255, "A")]   // BGR (0,0,255) = pure red → ANOTHER
    [InlineData(0, 255, 255, "H")] // pure yellow → HYPER
    [InlineData(0, 255, 0, "B")]   // pure green → BEGINNER
    [InlineData(255, 0, 0, "N")]   // pure blue → NORMAL
    [InlineData(255, 0, 255, "L")] // pure magenta/purple → LEGGENDARIA
    public void DetectDifficulty_PureColor_ReturnsExpectedLabel(byte b, byte g, byte r, string expected)
    {
        using var mat = MakeSolid(64, 32, b, g, r);
        var detector = new ColorDifficultyDetector();

        var result = detector.DetectDifficulty(mat);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectDifficulty_BlackImage_ReturnsNull()
    {
        using var mat = MakeSolid(64, 32, 0, 0, 0);
        var detector = new ColorDifficultyDetector();
        Assert.Null(detector.DetectDifficulty(mat));
    }

    [Fact]
    public void DetectDifficulty_WhiteImage_ReturnsNull()
    {
        // 白は S=0 で全ピクセルが彩度しきい値未満
        using var mat = MakeSolid(64, 32, 255, 255, 255);
        var detector = new ColorDifficultyDetector();
        Assert.Null(detector.DetectDifficulty(mat));
    }

    [Fact]
    public void DetectDifficulty_EmptyMat_ReturnsNull()
    {
        var detector = new ColorDifficultyDetector();
        Assert.Null(detector.DetectDifficulty(new Mat()));
    }

    [Fact]
    public void ComputeBandStats_PureBlue_ReportsHighRatio()
    {
        using var mat = MakeSolid(64, 32, 255, 0, 0);
        var detector = new ColorDifficultyDetector();

        var stats = detector.ComputeBandStats(mat);

        Assert.Equal("N", stats.DominantDifficulty);
        Assert.True(stats.DominantRatio > 0.95, $"期待: >0.95、実測: {stats.DominantRatio}");
    }

    [Fact]
    public void DetectDifficulty_BelowMinDominantRatio_ReturnsNull()
    {
        // ほぼ黒に近く彩度の高いピクセルが少ない画像
        using var mat = MakeSolid(64, 32, 5, 5, 5);
        var detector = new ColorDifficultyDetector(
            DefaultDifficultyColorPalette.Bands,
            minDominantRatio: 0.5);
        Assert.Null(detector.DetectDifficulty(mat));
    }
}
