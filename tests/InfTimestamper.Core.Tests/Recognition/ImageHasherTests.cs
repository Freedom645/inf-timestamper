using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class ImageHasherTests
{
    [Fact]
    public void ComputeHashes_IdenticalImages_HaveZeroHammingDistance()
    {
        using var mat = MakeGradient();
        var hasher = new ImageHasher();
        var a1 = hasher.ComputeAverageHash(mat);
        var a2 = hasher.ComputeAverageHash(mat);
        Assert.Equal(0, ImageHasher.HammingDistance(a1, a2));

        var p1 = hasher.ComputePerceptualHash(mat);
        var p2 = hasher.ComputePerceptualHash(mat);
        Assert.Equal(0, ImageHasher.HammingDistance(p1, p2));
    }

    [Fact]
    public void ComputeHashes_DifferentImages_HaveLargeDistance()
    {
        // 均一塗りつぶし同士は aHash で判別不能（平均との大小比較が均一）になるため
        // 横方向グラデーションと縦方向グラデーションを比較する
        using var horizontal = MakeGradient(horizontal: true);
        using var vertical = MakeGradient(horizontal: false);

        var hasher = new ImageHasher();
        var hH = hasher.ComputeAverageHash(horizontal);
        var vH = hasher.ComputeAverageHash(vertical);

        // 構造が直交していれば、aHash には少なくとも 10bit 以上の違いが出るはず
        Assert.True(
            ImageHasher.HammingDistance(hH, vH) >= 10,
            $"aHash 距離が想定より小さい: {ImageHasher.HammingDistance(hH, vH)}");
    }

    [Fact]
    public void HammingDistance_DiffersBitwise()
    {
        Assert.Equal(0, ImageHasher.HammingDistance(0UL, 0UL));
        Assert.Equal(64, ImageHasher.HammingDistance(0UL, ulong.MaxValue));
        Assert.Equal(1, ImageHasher.HammingDistance(0UL, 1UL));
        Assert.Equal(2, ImageHasher.HammingDistance(0b0011UL, 0b0000UL));
    }

    private static Mat MakeGradient() => MakeGradient(horizontal: true);

    private static Mat MakeGradient(bool horizontal)
    {
        var mat = new Mat(64, 64, MatType.CV_8UC3);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            byte value = (byte)((horizontal ? x : y) * 4);
            mat.Set(y, x, new Vec3b(value, value, value));
        }
        return mat;
    }
}
