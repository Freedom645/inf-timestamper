using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// 1P/2P サイド自動判定と side-aware な ROI 選択のテスト。
/// state ハッシュ名 (例: "result/2p_marker") から DetectedSide を抽出し、
/// lamp_color_1p / lamp_color_2p のうち適切な ROI を使うことを検証する。
/// </summary>
public class FrameRecognizerSideTests
{
    private static Mat MakeSolidArea(int width, int height, Scalar color)
        => new(height, width, MatType.CV_8UC3, color);

    private static void Paint(Mat frame, Roi roi, Scalar color)
    {
        using var dst = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        dst.SetTo(color);
    }

    /// <summary>
    /// 指定 state name (例: "1p_marker") と Result ROI / 1P・2P ランプ ROI を埋め込んだ
    /// 合成フレーム + Recognizer を構築する。
    /// 1P 領域は緑 (EASY)、2P 領域は赤 (HARD) に塗る。
    /// </summary>
    private static (FrameRecognizer recognizer, Mat frame) BuildScenario(
        string stateEntryName,
        Scalar onePColor,
        Scalar twoPColor)
    {
        var hasher = new ImageHasher();

        var stateRoi = new Roi(0, 0, 16, 16);
        var lamp1P = new Roi(100, 100, 50, 17);
        var lamp2P = new Roi(1700, 100, 50, 17);

        var frame = new Mat(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 0));

        // 状態判定領域: gray
        Paint(frame, stateRoi, new Scalar(128, 128, 128));
        ulong stateHash;
        using (var sub = new Mat(frame, new Rect(stateRoi.X, stateRoi.Y, stateRoi.Width, stateRoi.Height)))
            stateHash = hasher.ComputeAverageHash(sub);

        // ランプ ROI: 1P/2P で異なる色
        Paint(frame, lamp1P, onePColor);
        Paint(frame, lamp2P, twoPColor);

        var hashes = new HashResource
        {
            States = new Dictionary<string, IReadOnlyList<StateHashEntry>>
            {
                ["result"] = new List<StateHashEntry>
                {
                    new(stateEntryName, stateRoi, stateHash, 5),
                },
            },
        };

        var rois = new RoiResource
        {
            Rois = new Dictionary<string, Roi>
            {
                [RecognitionRoiKeys.LampColor1P] = lamp1P,
                [RecognitionRoiKeys.LampColor2P] = lamp2P,
            },
        };

        var recognizer = new FrameRecognizer(hasher, new NoOpOcrService(), hashes, rois);
        return (recognizer, frame);
    }

    [Fact]
    public void Recognize_StateNameContains1p_DetectsOnePAndUses1PLampRoi()
    {
        // 1P 領域=赤 (HARD), 2P 領域=緑 (EASY)
        var (recognizer, frame) = BuildScenario(
            "1p_marker",
            onePColor: new Scalar(0, 0, 255),   // BGR red
            twoPColor: new Scalar(0, 255, 0));   // BGR green
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.Result, result.State);
            Assert.Equal(PlaySide.OneP, result.DetectedSide);
            // 1P サイドの赤を読んで HARD と判定される
            Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_StateNameContains2p_DetectsTwoPAndUses2PLampRoi()
    {
        // 1P 領域=赤 (HARD), 2P 領域=緑 (EASY) → 2P 選択で EASY になるはず
        var (recognizer, frame) = BuildScenario(
            "2p_marker",
            onePColor: new Scalar(0, 0, 255),
            twoPColor: new Scalar(0, 255, 0));
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.Result, result.State);
            Assert.Equal(PlaySide.TwoP, result.DetectedSide);
            Assert.Equal("EASY", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_StateNameNoSideMarker_DetectedSideIsUnknown()
    {
        var (recognizer, frame) = BuildScenario(
            "neutral_marker",
            onePColor: new Scalar(0, 0, 255),
            twoPColor: new Scalar(0, 255, 0));
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

            Assert.Equal(RecognizedState.Result, result.State);
            Assert.Equal(PlaySide.Unknown, result.DetectedSide);
            // Unknown のとき RecognitionRoiKeys.LampColor は 2P を既定として返すので EASY (緑) になる
            Assert.Equal("EASY", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_StateNameNoSide_HintSideOverrides()
    {
        // hintSide=OneP を渡すと、state name 由来 side が Unknown でも 1P ROI が使われる
        var (recognizer, frame) = BuildScenario(
            "neutral_marker",
            onePColor: new Scalar(0, 0, 255),   // red → HARD
            twoPColor: new Scalar(0, 255, 0));
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now, hintSide: PlaySide.OneP);

            // state name 由来は Unknown (frame 単体での検出結果) のまま
            Assert.Equal(PlaySide.Unknown, result.DetectedSide);
            // 抽出には hintSide が適用され、1P 側の赤を見て HARD
            Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void Recognize_DetectedSideTakesPrecedenceOverHint()
    {
        // hintSide=2P を渡しても state name "1p_marker" が優先される
        var (recognizer, frame) = BuildScenario(
            "1p_marker",
            onePColor: new Scalar(0, 0, 255),
            twoPColor: new Scalar(0, 255, 0));
        try
        {
            var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now, hintSide: PlaySide.TwoP);

            Assert.Equal(PlaySide.OneP, result.DetectedSide);
            Assert.Equal("HARD", result.Fields[RecognitionFieldKeys.Lamp]);
        }
        finally { frame.Dispose(); }
    }
}
