using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace InfTimestamper.Core.Recognition;

public sealed class ImageHasher : IImageHasher
{
    private readonly IImageHash _aHash = new AverageHash();
    private readonly IImageHash _pHash = new PerceptualHash();

    public ulong ComputeAverageHash(Mat roi) => HashWith(_aHash, roi);
    public ulong ComputePerceptualHash(Mat roi) => HashWith(_pHash, roi);

    public static int HammingDistance(ulong a, ulong b)
        => System.Numerics.BitOperations.PopCount(a ^ b);

    private static ulong HashWith(IImageHash algo, Mat roi)
    {
        if (roi is null || roi.Empty())
            throw new ArgumentException("ROI が空です。", nameof(roi));

        var bytes = roi.ImEncode(".png");
        using var image = Image.Load<Rgba32>(bytes);
        return algo.Hash(image);
    }
}
