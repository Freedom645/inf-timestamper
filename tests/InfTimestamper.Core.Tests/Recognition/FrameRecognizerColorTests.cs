using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

public class FrameRecognizerColorTests
{
    /// <summary>
    /// SongSelect 状態と判定される画像を合成 + 指定 ROI に純色を塗って、
    /// 色判定で diff_s が SP + 色短縮になることを検証する。
    /// </summary>
    private static (FrameRecognizer recognizer, Mat frame) BuildScenario(byte b, byte g, byte r)
    {
        // SongSelect 用の参照ハッシュ (合成画像と一致するように後で計算する)
        var stateRoi = new Roi(0, 0, 16, 16);
        var difficultyRoi = new Roi(100, 100, 64, 32);

        // 合成 1920x1080 フレーム
        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 0));

        // 状態判定領域: 単色グレー (aHash 0 にしておく)
        using (var stateRegion = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateRegion.SetTo(new Scalar(128, 128, 128));

        // 難易度色 ROI: 指定色で塗る
        using (var diffRegion = new Mat(frame, new Rect(difficultyRoi.X, difficultyRoi.Y, difficultyRoi.Width, difficultyRoi.Height)))
            diffRegion.SetTo(new Scalar(b, g, r));

        // 状態 ROI の aHash を計算（フレーム全体ベース、ROI 切り出し → aHash）
        var hasher = new ImageHasher();
        using (var stateSub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
        {
            var stateHash = hasher.ComputeAverageHash(stateSub);
            var hashes = new HashResource
            {
                States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
                {
                    [RecognizedStateNames.SongSelect] = new List<StateHashEntry>
                    {
                        new("synthetic", stateRoi, stateHash, 5),
                    },
                },
            };

            var rois = new RoiResource
            {
                Rois = new Dictionary<string, Roi>
                {
                    [RecognitionRoiKeys.DifficultyColor] = difficultyRoi,
                },
            };

            var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, rois);
            return (recognizer, frame);
        }
    }

    [Theory]
    [InlineData(0, 0, 255, "SPA", "ANOTHER")]
    [InlineData(0, 255, 255, "SPH", "HYPER")]
    [InlineData(0, 255, 0, "SPB", "BEGINNER")]
    [InlineData(255, 0, 0, "SPN", "NORMAL")]
    [InlineData(255, 0, 255, "SPL", "LEGGENDARIA")]
    public void Recognize_SongSelectWithColorRoi_PopulatesDiffShortAndLong(
        byte b, byte g, byte r, string expectedShort, string expectedLong)
    {
        var (recognizer, frame) = BuildScenario(b, g, r);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.SongSelect, result.State);
            Assert.Equal(expectedShort, result.Fields[RecognitionFieldKeys.DiffShort]);
            Assert.Equal(expectedLong, result.Fields[RecognitionFieldKeys.DiffLong]);
        }
        finally
        {
            frame.Dispose();
        }
    }
}
