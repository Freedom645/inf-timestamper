using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// 状態別フィールド抽出のテスト。PlayStart 状態ではフィールド抽出をスキップし、
/// SongSelect で蓄積した値が Pipeline 側で merge されることを前提とする。
/// </summary>
public class FrameRecognizerStateExtractionTests
{
    private static (FrameRecognizer recognizer, Mat frame) BuildScenarioForState(string stateName)
    {
        var hasher = new ImageHasher();
        var stateRoi = new Roi(0, 0, 16, 16);
        var difficultyRoi = new Roi(100, 100, 64, 32);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 0));
        using (var stateRegion = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateRegion.SetTo(new Scalar(128, 128, 128));
        using (var diffRegion = new Mat(frame, new Rect(difficultyRoi.X, difficultyRoi.Y, difficultyRoi.Width, difficultyRoi.Height)))
            diffRegion.SetTo(new Scalar(0, 0, 255)); // BGR red → A (ANOTHER)

        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                [stateName] = new List<StateHashEntry> { new("synthetic", stateRoi, stateHash, 5) },
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

    [Fact]
    public void Recognize_SongSelect_ExtractsDifficulty()
    {
        var (recognizer, frame) = BuildScenarioForState(RecognizedStateNames.SongSelect);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.SongSelect, result.State);
            Assert.Equal("SPA", result.Fields[RecognitionFieldKeys.DiffShort]);
            Assert.Equal("ANOTHER", result.Fields[RecognitionFieldKeys.DiffLong]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_PlayStart_DoesNotExtractFields()
    {
        // PlayStart 画面は 1P/2P で大きく異なるレイアウト (ノーツ降下開始 + 左右ミラー) のため、
        // SongSelect 用の ROI で OCR/色判定を走らせると正しい値を上書きしてしまう。
        // FrameRecognizer は PlayStart で fields を抽出しないことを検証する。
        var (recognizer, frame) = BuildScenarioForState(RecognizedStateNames.PlayStart);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.PlayStart, result.State);
            Assert.Empty(result.Fields);
        }
        finally { frame.Dispose(); }
    }
}
