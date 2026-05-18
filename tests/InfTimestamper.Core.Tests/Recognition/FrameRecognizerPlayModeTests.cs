using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// hashes.json の "play_mode" セクションを使った SP/DP プレフィックス検出のテスト。
/// 検出できればその prefix が、できなければ DefaultPlayModePrefix ("SP") が使われる。
/// </summary>
public class FrameRecognizerPlayModeTests
{
    private static void Paint(Mat frame, Roi roi, Scalar color)
    {
        using var dst = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        dst.SetTo(color);
    }

    private static Mat MakeGradient(int w, int h)
    {
        var mat = new Mat(h, w, MatType.CV_8UC3);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte v = (byte)((x + y) * 2);
            mat.Set(y, x, new Vec3b(v, v, v));
        }
        return mat;
    }

    [Fact]
    public void Recognize_PlayModeMatched_PrependsDetectedPrefixToDiffShort()
    {
        var (recognizer, frame) = BuildScenario("DP");
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);
            // SongSelect 状態 + 赤色 difficulty → "DPA" (DP + Anotherの A)
            Assert.Equal("DPA", result.Fields[RecognitionFieldKeys.DiffShort]);
            Assert.Equal("ANOTHER", result.Fields[RecognitionFieldKeys.DiffLong]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_PlayModeUnmatched_FallsBackToDefaultPrefix()
    {
        // play_mode セクションが空 → DefaultPlayModePrefix ("SP") が使われる
        var (recognizer, frame) = BuildScenario(null);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);
            Assert.Equal("SPA", result.Fields[RecognitionFieldKeys.DiffShort]);
        }
        finally { frame.Dispose(); }
    }

    private static (FrameRecognizer recognizer, Mat frame) BuildScenario(string? playModeValue)
    {
        var hasher = new ImageHasher();

        var stateRoi = new Roi(0, 0, 16, 16);
        var diffColorRoi = new Roi(100, 100, 64, 32);
        var playModeRoi = new Roi(200, 200, 64, 64);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0));

        // 状態判定: SongSelect (gray)
        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        // 難易度色 ROI: 赤 (BGR=0,0,255) → A (ANOTHER)
        Paint(frame, diffColorRoi, new Scalar(0, 0, 255));

        // play_mode ROI: gradient (一意な aHash を生成)
        using (var gradient = MakeGradient(playModeRoi.Width, playModeRoi.Height))
        using (var dst = new Mat(frame, new Rect(playModeRoi.X, playModeRoi.Y, playModeRoi.Width, playModeRoi.Height)))
            gradient.CopyTo(dst);

        ulong playModeHash;
        using (var sub = new Mat(frame, new Rect(playModeRoi.X, playModeRoi.Y, playModeRoi.Width, playModeRoi.Height)))
            playModeHash = hasher.ComputeAverageHash(sub);

        var playModeEntries = playModeValue is null
            ? new List<IconHashEntry>()
            : new List<IconHashEntry> { new(playModeValue, playModeRoi, playModeHash, 5) };

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["song_select"] = new List<StateHashEntry> { new("marker", stateRoi, stateHash, 5) },
            },
            PlayMode = playModeEntries,
        };

        var rois = new RoiResource
        {
            Rois = new Dictionary<string, Roi>
            {
                [RecognitionRoiKeys.DifficultyColor] = diffColorRoi,
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, rois);
        return (recognizer, frame);
    }
}
