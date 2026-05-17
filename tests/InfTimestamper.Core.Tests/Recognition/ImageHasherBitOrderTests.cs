using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// Python imagehash と CoenM.ImageSharp.ImageHash の AverageHash 互換性検証。
/// 両者は仕様としては「8x8 グレースケール、平均値で 2 値化」で同じだが、
/// 64bit 整数へのパッキング順序（MSB ファースト / LSB ファースト）が
/// 一致するかを単純な合成画像で検証する。
///
/// Python imagehash の hash_to_int は最初のピクセル (左上) を MSB に置く方式:
///   h = 0
///   for row in self.hash:
///     for bit in row:
///       h = (h << 1) | int(bit)
///
/// よって左上から右下にかけて読んだ順で MSB から並ぶ。
/// </summary>
public class ImageHasherBitOrderTests
{
    [Fact]
    public void AverageHash_TopHalfWhite_HasUpperBitsSet()
    {
        // 上半分（32行）白、下半分（32行）黒。
        // 8x8 にダウンサンプル後、上 4 行が白・下 4 行が黒になる。
        // 行優先 MSB-first パッキングなら 0xFFFFFFFF00000000 が期待値。
        using var mat = new Mat(64, 64, MatType.CV_8UC3, new Scalar(0, 0, 0));
        using (var top = new Mat(mat, new Rect(0, 0, 64, 32)))
            top.SetTo(new Scalar(255, 255, 255));

        var hash = new ImageHasher().ComputeAverageHash(mat);

        Assert.Equal(0xFFFFFFFF00000000UL, hash);
    }

    [Fact]
    public void AverageHash_LeftHalfWhite_HasAlternatingNibbles()
    {
        // 左半分（32列）白、右半分（32列）黒。
        // 8x8 ダウンサンプル後、各行 [W W W W B B B B] となる。
        // MSB-first パックなら各行が 0b11110000 = 0xF0。
        // 全 8 行で 0xF0F0F0F0F0F0F0F0。
        using var mat = new Mat(64, 64, MatType.CV_8UC3, new Scalar(0, 0, 0));
        using (var left = new Mat(mat, new Rect(0, 0, 32, 64)))
            left.SetTo(new Scalar(255, 255, 255));

        var hash = new ImageHasher().ComputeAverageHash(mat);

        Assert.Equal(0xF0F0F0F0F0F0F0F0UL, hash);
    }
}
