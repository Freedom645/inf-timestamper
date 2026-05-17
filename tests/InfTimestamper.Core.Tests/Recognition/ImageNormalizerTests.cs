using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class ImageNormalizerTests
{
    [Fact]
    public void Normalize_NativeResolution_ReturnsCloneAtTargetSize()
    {
        using var mat = new Mat(ImageNormalizer.TargetHeight, ImageNormalizer.TargetWidth, MatType.CV_8UC3, new Scalar(128, 128, 128));
        var bytes = mat.ImEncode(".png");

        var normalizer = new ImageNormalizer();
        using var result = normalizer.Normalize(bytes);

        Assert.Equal(ImageNormalizer.TargetWidth, result.Width);
        Assert.Equal(ImageNormalizer.TargetHeight, result.Height);
    }

    [Fact]
    public void Normalize_Width1360Height1080_PadsHorizontally()
    {
        using var mat = new Mat(1080, 1360, MatType.CV_8UC3, new Scalar(255, 255, 255));
        var bytes = mat.ImEncode(".png");

        var normalizer = new ImageNormalizer();
        using var result = normalizer.Normalize(bytes);

        Assert.Equal(ImageNormalizer.TargetWidth, result.Width);
        Assert.Equal(ImageNormalizer.TargetHeight, result.Height);

        // 左端 (0,540) は黒パディング
        var left = result.At<Vec3b>(540, 10);
        Assert.Equal(0, left.Item0);
        Assert.Equal(0, left.Item1);
        Assert.Equal(0, left.Item2);

        // 中央 (960,540) は白（元画像領域）
        var center = result.At<Vec3b>(540, 960);
        Assert.Equal(255, center.Item0);
        Assert.Equal(255, center.Item1);
        Assert.Equal(255, center.Item2);
    }

    [Fact]
    public void Normalize_HdResolution_ResizesToTarget()
    {
        using var mat = new Mat(720, 1280, MatType.CV_8UC3, new Scalar(200, 100, 50));
        var bytes = mat.ImEncode(".png");

        var normalizer = new ImageNormalizer();
        using var result = normalizer.Normalize(bytes);

        Assert.Equal(ImageNormalizer.TargetWidth, result.Width);
        Assert.Equal(ImageNormalizer.TargetHeight, result.Height);

        // 16:9 のリサイズなのでパディングは無く、中央は元色に近い
        var center = result.At<Vec3b>(540, 960);
        Assert.InRange(center.Item0, 190, 210);
        Assert.InRange(center.Item1, 90, 110);
        Assert.InRange(center.Item2, 40, 60);
    }

    [Fact]
    public void Normalize_FourThreeAspect_LetterboxesHorizontally()
    {
        // 4:3 (1280x960) → リサイズ後 1440x1080、左右レターボックスで 1920x1080
        using var mat = new Mat(960, 1280, MatType.CV_8UC3, new Scalar(0, 200, 0));
        var bytes = mat.ImEncode(".png");

        var normalizer = new ImageNormalizer();
        using var result = normalizer.Normalize(bytes);

        Assert.Equal(ImageNormalizer.TargetWidth, result.Width);
        Assert.Equal(ImageNormalizer.TargetHeight, result.Height);

        // 左端 10px は黒レターボックス
        var left = result.At<Vec3b>(540, 10);
        Assert.Equal(0, left.Item0);
        Assert.Equal(0, left.Item1);
        Assert.Equal(0, left.Item2);

        // 中央 (960,540) は元色 (0,200,0)
        var center = result.At<Vec3b>(540, 960);
        Assert.InRange(center.Item1, 190, 210);
    }

    [Fact]
    public void Normalize_EmptyBytes_Throws()
    {
        var normalizer = new ImageNormalizer();
        Assert.Throws<InvalidDataException>(() => normalizer.Normalize(Array.Empty<byte>()));
    }
}
