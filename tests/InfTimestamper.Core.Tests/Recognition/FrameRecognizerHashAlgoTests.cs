using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// IconHashEntry の Algo (aHash/pHash) と Side フィルタリングをカバーするテスト。
/// </summary>
public class FrameRecognizerHashAlgoTests
{
    private static void Paint(Mat frame, Roi roi, Scalar color)
    {
        using var dst = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        dst.SetTo(color);
    }

    private static Mat MakeGradient(int w, int h, int offset = 0)
    {
        var mat = new Mat(h, w, MatType.CV_8UC3);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte v = (byte)((x + y + offset) * 2);
            mat.Set(y, x, new Vec3b(v, v, v));
        }
        return mat;
    }

    /// <summary>
    /// IconHashEntry.Algo=Perceptual のエントリは pHash で照合される。
    /// </summary>
    [Fact]
    public void MatchIcons_PerceptualEntry_UsesPerceptualHash()
    {
        var hasher = new ImageHasher();
        var stateRoi = new Roi(0, 0, 16, 16);
        var iconRoi = new Roi(500, 500, 64, 64);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));
        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        using (var gradient = MakeGradient(iconRoi.Width, iconRoi.Height))
        using (var dst = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            gradient.CopyTo(dst);

        // 同 ROI から計算した pHash を期待値とする
        ulong refPHash;
        using (var sub = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            refPHash = hasher.ComputePerceptualHash(sub);

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["result"] = new List<StateHashEntry> { new("marker", stateRoi, stateHash, 5) },
            },
            DjLevel = new List<IconHashEntry>
            {
                new("AAA", iconRoi, refPHash, 5, HashAlgorithm.Perceptual),
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, RoiResource.Empty());
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);
            Assert.Equal("AAA", result.Fields[RecognitionFieldKeys.DjLevel]);
        }
        finally { frame.Dispose(); }
    }

    /// <summary>
    /// Side が指定されたエントリは current side と一致しない場合スキップされる。
    /// </summary>
    [Fact]
    public void MatchIcons_SideMismatch_SkipsEntry()
    {
        var hasher = new ImageHasher();
        var stateRoi = new Roi(0, 0, 16, 16);
        var iconRoi = new Roi(500, 500, 64, 64);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));
        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        using (var gradient = MakeGradient(iconRoi.Width, iconRoi.Height))
        using (var dst = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            gradient.CopyTo(dst);

        ulong refHash;
        using (var sub = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            refHash = hasher.ComputeAverageHash(sub);

        var hashes = new HashResource
        {
            // state 名から side=2P が検出される
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["result"] = new List<StateHashEntry> { new("2p_marker", stateRoi, stateHash, 5) },
            },
            // dj_level エントリは 1P 専用。2P フレームでは match しないはず
            DjLevel = new List<IconHashEntry>
            {
                new("AAA_1p_only", iconRoi, refHash, 5, HashAlgorithm.Average, PlaySide.OneP),
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, RoiResource.Empty());
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);
            Assert.False(result.Fields.ContainsKey(RecognitionFieldKeys.DjLevel));
        }
        finally { frame.Dispose(); }
    }

    /// <summary>
    /// Side=Unknown のエントリはどちらのサイドでも match する。
    /// </summary>
    [Fact]
    public void MatchIcons_SideUnknownEntry_MatchesAnySide()
    {
        var hasher = new ImageHasher();
        var stateRoi = new Roi(0, 0, 16, 16);
        var iconRoi = new Roi(500, 500, 64, 64);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));
        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        using (var gradient = MakeGradient(iconRoi.Width, iconRoi.Height))
        using (var dst = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            gradient.CopyTo(dst);

        ulong refHash;
        using (var sub = new Mat(frame, new Rect(iconRoi.X, iconRoi.Y, iconRoi.Width, iconRoi.Height)))
            refHash = hasher.ComputeAverageHash(sub);

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["result"] = new List<StateHashEntry> { new("2p_marker", stateRoi, stateHash, 5) },
            },
            // Side 未指定 (Unknown)
            DjLevel = new List<IconHashEntry>
            {
                new("any_side", iconRoi, refHash, 5),
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, RoiResource.Empty());
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);
            Assert.Equal("any_side", result.Fields[RecognitionFieldKeys.DjLevel]);
        }
        finally { frame.Dispose(); }
    }
}
