using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// HARD と FAILED の区別: FAILED 時のみ画面全体が赤いオーバーレイになることを利用する。
/// lamp_color ROI が赤と判定された後、failed_background ROI で背景の赤さを確認して
/// HARD/FAILED を分岐させる。
/// </summary>
public class FrameRecognizerFailedDetectionTests
{
    private static void Paint(Mat frame, Roi roi, Scalar color)
    {
        using var dst = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        dst.SetTo(color);
    }

    /// <summary>
    /// Result 状態 + 赤 lamp + 赤背景 → FAILED と判定されること。
    /// </summary>
    [Fact]
    public void Recognize_RedLampWithRedBackground_LabelsAsFailed()
    {
        var (recognizer, frame) = BuildScenario(lampRed: true, backgroundRed: true);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.Result, result.State);
            Assert.Equal("FAILED", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    /// <summary>
    /// Result 状態 + 赤 lamp + 非赤背景 → 通常の HARD と判定されること。
    /// </summary>
    [Fact]
    public void Recognize_RedLampWithNonRedBackground_LabelsAsHard()
    {
        var (recognizer, frame) = BuildScenario(lampRed: true, backgroundRed: false);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.Result, result.State);
            Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    /// <summary>
    /// failed_background ROI が rois.json に無いときは FAILED 判定スキップ → HARD のまま返る。
    /// </summary>
    [Fact]
    public void Recognize_NoFailedBackgroundRoi_DefaultsToHard()
    {
        var (recognizer, frame) = BuildScenario(lampRed: true, backgroundRed: true, includeFailedBgRoi: false);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    /// <summary>
    /// 非赤 lamp (例: 緑) のときは FAILED 判定をスキップ → そのままラベル返却。
    /// </summary>
    [Fact]
    public void Recognize_GreenLamp_BypassesFailedCheck()
    {
        var (recognizer, frame) = BuildScenario(lampRed: false, backgroundRed: true);
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            // 緑 lamp = EASY、背景が赤くてもラベルは EASY のまま
            Assert.Equal("EASY", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    private static (FrameRecognizer recognizer, Mat frame) BuildScenario(
        bool lampRed, bool backgroundRed, bool includeFailedBgRoi = true)
    {
        var hasher = new ImageHasher();

        var stateRoi = new Roi(0, 0, 16, 16);
        var lampRoi = new Roi(1700, 100, 50, 17);
        var bgRoi = new Roi(800, 100, 400, 200);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 0));

        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        // lamp ROI 内の色
        Paint(frame, lampRoi, lampRed ? new Scalar(0, 0, 255) : new Scalar(0, 255, 0));

        // 背景 ROI 内の色（FAILED 検出に使われる）
        Paint(frame, bgRoi, backgroundRed ? new Scalar(0, 0, 255) : new Scalar(0, 255, 0));

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["result"] = new List<StateHashEntry>
                {
                    new("2p_marker", stateRoi, stateHash, 5),
                },
            },
        };

        var roiDict = new Dictionary<string, Roi>
        {
            [RecognitionRoiKeys.LampColor2P] = lampRoi,
        };
        if (includeFailedBgRoi)
            roiDict[RecognitionRoiKeys.FailedBackground] = bgRoi;

        var rois = new RoiResource { Rois = roiDict };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, rois);
        return (recognizer, frame);
    }
}
