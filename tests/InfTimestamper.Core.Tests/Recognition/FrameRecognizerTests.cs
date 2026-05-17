using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class FrameRecognizerTests
{
    [Fact]
    public void Recognize_EmptyResources_ReturnsUnknownWithNoFields()
    {
        var recognizer = new FrameRecognizer(
            new ImageHasher(),
            new NoOpOcrService(),
            HashResource.Empty(),
            RoiResource.Empty());

        using var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 0));
        var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

        Assert.Equal(RecognizedState.Unknown, result.State);
        Assert.Null(result.StateMatch);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public void Recognize_MatchingStateHash_ReturnsSongSelect()
    {
        var hasher = new ImageHasher();
        var roi = new Roi(100, 100, 64, 64);

        // 参照画像（64x64 のグラデーション）を作ってハッシュ計算
        using var reference = MakeGradient();
        var referenceHash = hasher.ComputeAverageHash(reference);

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["song_select"] = new List<StateHashEntry>
                {
                    new("1p_controller", roi, referenceHash, Threshold: 5),
                },
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, RoiResource.Empty());

        // 1920x1080 フレームの ROI 部分に参照画像を貼り付け
        using var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));
        using (var dst = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height)))
        {
            reference.CopyTo(dst);
        }

        var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

        Assert.Equal(RecognizedState.SongSelect, result.State);
        Assert.NotNull(result.StateMatch);
        Assert.Equal(0, result.StateMatch!.Distance);
    }

    [Fact]
    public void Recognize_DifficultyIconMatch_PopulatesDiffSAndDiffL()
    {
        var hasher = new ImageHasher();
        var diffRoi = new Roi(500, 500, 64, 64);

        using var reference = MakeGradient();
        var refHash = hasher.ComputeAverageHash(reference);

        // 状態判定用にも song_select エントリを置く（同 ROI / 同ハッシュ）
        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["song_select"] = new List<StateHashEntry>
                {
                    new("1p", diffRoi, refHash, 5),
                },
            },
            Difficulty = new List<IconHashEntry>
            {
                new("SPA", diffRoi, refHash, 5),
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, RoiResource.Empty());

        using var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));
        using (var dst = new Mat(frame, new Rect(diffRoi.X, diffRoi.Y, diffRoi.Width, diffRoi.Height)))
        {
            reference.CopyTo(dst);
        }

        var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

        Assert.Equal(RecognizedState.SongSelect, result.State);
        Assert.Equal("SPA", result.Fields[RecognitionFieldKeys.DiffShort]);
        Assert.Equal("ANOTHER", result.Fields[RecognitionFieldKeys.DiffLong]);
    }

    [Theory]
    [InlineData("SPB", "BEGINNER")]
    [InlineData("SPN", "NORMAL")]
    [InlineData("SPH", "HYPER")]
    [InlineData("SPA", "ANOTHER")]
    [InlineData("SPL", "LEGGENDARIA")]
    [InlineData("DPL", "LEGGENDARIA")]
    [InlineData("", "")]
    [InlineData("XXX", "")]
    public void DifficultyShortToLong_MapsLastLetter(string input, string expected)
    {
        Assert.Equal(expected, FrameRecognizer.DifficultyShortToLong(input));
    }

    [Fact]
    public void Recognize_FromObsScreenshot_PassesCapturedAtThrough()
    {
        var recognizer = new FrameRecognizer(
            new ImageHasher(), new NoOpOcrService(), HashResource.Empty(), RoiResource.Empty());

        using var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(120));
        var pngBytes = frame.ImEncode(".png");
        var capturedAt = new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9));

        var result = recognizer.Recognize(new ObsScreenshot(pngBytes, capturedAt));

        Assert.Equal(capturedAt, result.CapturedAt);
    }

    private static Mat MakeGradient()
    {
        var mat = new Mat(64, 64, MatType.CV_8UC3);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            byte v = (byte)((x + y) * 2);
            mat.Set(y, x, new Vec3b(v, v, v));
        }
        return mat;
    }
}
