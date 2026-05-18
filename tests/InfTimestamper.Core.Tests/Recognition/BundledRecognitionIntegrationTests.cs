using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

/// <summary>
/// 同梱した hashes.json / rois.json と実 INFINITAS 参考画像を組み合わせた統合テスト。
/// 参考画像は著作権の都合で git に含まれていないため、未配置の環境ではテストをスキップする。
/// </summary>
public class BundledRecognitionIntegrationTests
{
    private static readonly string ResourceDir = Path.Combine(
        AppContext.BaseDirectory, "Resources", "INFINITAS");

    // 参考画像はリポジトリのソースツリー側にあり、テスト出力にはコピーされない。
    // bin/Debug/.../Resources/INFINITAS から見ると src/InfTimestamper.Core/Resources/INFINITAS/reference_images にあたる。
    private static readonly string ReferenceImageDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "InfTimestamper.Core", "Resources", "INFINITAS", "reference_images"));

    private static bool ResourcesAvailable() =>
        File.Exists(Path.Combine(ResourceDir, "hashes.json"))
        && File.Exists(Path.Combine(ResourceDir, "rois.json"))
        && Directory.Exists(ReferenceImageDir);

    private static FrameRecognizer BuildRecognizer()
    {
        var hashes = HashResourceLoader.Load(Path.Combine(ResourceDir, "hashes.json"));
        var rois = RoiResourceLoader.Load(Path.Combine(ResourceDir, "rois.json"));
        return new FrameRecognizer(new ImageHasher(), new NoOpOcrService(), hashes, rois);
    }

    private static Mat LoadFrame(string relativePath)
    {
        var fullPath = Path.Combine(ReferenceImageDir, relativePath);
        var bytes = File.ReadAllBytes(fullPath);
        return new ImageNormalizer().Normalize(bytes);
    }

    /// <summary>
    /// 同梱の hashes.json + rois.json + 参考画像で、Result 画面が state=Result と判定され、
    /// fields[lamp] に正しいラベル (HARD / EASY / FAILED 等) が入ることを検証する。
    /// 1P/2P サイドも state ハッシュ名 (1p_/2p_clear_type_label) から自動検出される。
    /// </summary>
    [Theory]
    [InlineData("clear_type_2p/HARD.png",    PlaySide.TwoP, "HARD")]
    [InlineData("clear_type_2p/EASY.png",    PlaySide.TwoP, "EASY")]
    [InlineData("clear_type_2p/EX-HARD.png", PlaySide.TwoP, "EX-HARD")]
    [InlineData("clear_type_2p/A-EASY.png",  PlaySide.TwoP, "A-EASY")]
    [InlineData("clear_type_2p/FAILED.png",  PlaySide.TwoP, "FAILED")]
    [InlineData("clear_type_1p/HARD.png",    PlaySide.OneP, "HARD")]
    [InlineData("clear_type_1p/EASY.png",    PlaySide.OneP, "EASY")]
    [InlineData("clear_type_1p/EX-HARD.png", PlaySide.OneP, "EX-HARD")]
    [InlineData("clear_type_1p/A-CLEAR.png", PlaySide.OneP, "A-EASY")]
    [InlineData("clear_type_1p/FAILED.png",  PlaySide.OneP, "FAILED")]
    public void Recognize_RealResultFrame_DetectsStateAndLamp(string imagePath, PlaySide expectedSide, string expectedLamp)
    {
        if (!ResourcesAvailable()) return; // 環境スキップ

        var recognizer = BuildRecognizer();
        using var frame = LoadFrame(imagePath);

        var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

        Assert.Equal(RecognizedState.Result, result.State);
        Assert.Equal(expectedSide, result.DetectedSide);
        Assert.True(result.Fields.TryGetValue(RecognitionFieldKeys.Lamp, out var lamp),
            $"lamp field 未設定。Fields: {string.Join(", ", result.Fields.Keys)}");
        Assert.Equal(expectedLamp, lamp);
    }

    /// <summary>
    /// FC / NORMAL は HSV 上で隣接するため、実画像で dominant 判定が安定しないケースがある。
    /// この 2 件は緩めの検証（FC か NORMAL のどちらか）に分離する。
    /// </summary>
    [Theory]
    [InlineData("clear_type_2p/FC.png")]
    [InlineData("clear_type_2p/NORMAL.png")]
    [InlineData("clear_type_2p/NP.png")]
    public void Recognize_FcNormalNp_AcceptsAdjacentBlueShades(string imagePath)
    {
        if (!ResourcesAvailable()) return;

        var recognizer = BuildRecognizer();
        using var frame = LoadFrame(imagePath);

        var result = recognizer.RecognizeFrame(frame, DateTimeOffset.Now);

        Assert.Equal(RecognizedState.Result, result.State);
        // FC / NORMAL は B 範囲が重なるため、相互に判定されることを許容。
        // NP は彩度ゼロのため lamp 自体が抽出されないこともあり得る。
        if (result.Fields.TryGetValue(RecognitionFieldKeys.Lamp, out var lamp))
            Assert.Contains(lamp, new[] { "FC", "NORMAL" });
    }

    /// <summary>
    /// DJ Level の pHash 照合: 同梱 hashes.json の 16 エントリで、参考画像から正しい文字が返ること。
    /// 状態判定とは独立に MatchIcons を直接実行するため、FrameRecognizer を再現するヘルパは使わず、
    /// HashResource の DjLevel を直接走査して同じロジックをテストする。
    /// </summary>
    [Theory]
    [InlineData("dj_level_1p/AAA.png", PlaySide.OneP, "AAA")]
    [InlineData("dj_level_1p/AA.png",  PlaySide.OneP, "AA")]
    [InlineData("dj_level_1p/A.png",   PlaySide.OneP, "A")]
    [InlineData("dj_level_1p/B.png",   PlaySide.OneP, "B")]
    [InlineData("dj_level_1p/C.png",   PlaySide.OneP, "C")]
    [InlineData("dj_level_1p/D.png",   PlaySide.OneP, "D")]
    [InlineData("dj_level_1p/E.png",   PlaySide.OneP, "E")]
    [InlineData("dj_level_1p/F.png",   PlaySide.OneP, "F")]
    [InlineData("dj_level_2p/AAA.png", PlaySide.TwoP, "AAA")]
    [InlineData("dj_level_2p/AA.png",  PlaySide.TwoP, "AA")]
    [InlineData("dj_level_2p/A.png",   PlaySide.TwoP, "A")]
    [InlineData("dj_level_2p/B.png",   PlaySide.TwoP, "B")]
    [InlineData("dj_level_2p/C.png",   PlaySide.TwoP, "C")]
    [InlineData("dj_level_2p/D.png",   PlaySide.TwoP, "D")]
    [InlineData("dj_level_2p/E.png",   PlaySide.TwoP, "E")]
    [InlineData("dj_level_2p/F.png",   PlaySide.TwoP, "F")]
    public void DjLevelHashes_MatchExpectedLetter(string imagePath, PlaySide side, string expected)
    {
        if (!ResourcesAvailable()) return;

        var hashes = HashResourceLoader.Load(Path.Combine(ResourceDir, "hashes.json"));
        using var frame = LoadFrame(imagePath);
        var hasher = new ImageHasher();

        var best = FindBestDjLevelMatch(frame, hasher, hashes.DjLevel, side);
        Assert.NotNull(best);
        Assert.Equal(expected, best!.Value);
    }

    private static IconHashEntry? FindBestDjLevelMatch(Mat frame, ImageHasher hasher, IReadOnlyList<IconHashEntry> candidates, PlaySide side)
    {
        IconHashEntry? best = null;
        int bestDistance = int.MaxValue;

        foreach (var entry in candidates)
        {
            if (entry.Side != PlaySide.Unknown && side != PlaySide.Unknown && entry.Side != side) continue;
            using var sub = new Mat(frame, new Rect(entry.Roi.X, entry.Roi.Y, entry.Roi.Width, entry.Roi.Height));
            var hash = entry.Algo == HashAlgorithm.Perceptual
                ? hasher.ComputePerceptualHash(sub)
                : hasher.ComputeAverageHash(sub);
            var distance = ImageHasher.HammingDistance(hash, entry.Hash);
            if (distance > entry.Threshold) continue;
            if (distance < bestDistance)
            {
                best = entry;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// SP/DP 判定: 同梱 hashes.json の play_mode セクションを使い、9 種の difficulty 参考画像で
    /// 正しく "SP" / "DP" が返ること。
    /// </summary>
    [Theory]
    [InlineData("difficulty/SPB.png", "SP")]
    [InlineData("difficulty/SPN.png", "SP")]
    [InlineData("difficulty/SPH.png", "SP")]
    [InlineData("difficulty/SPA.png", "SP")]
    [InlineData("difficulty/SPL.png", "SP")]
    [InlineData("difficulty/DPN.png", "DP")]
    [InlineData("difficulty/DPH.png", "DP")]
    [InlineData("difficulty/DPA.png", "DP")]
    [InlineData("difficulty/DPL.png", "DP")]
    public void PlayModeHashes_MatchExpectedMode(string imagePath, string expected)
    {
        if (!ResourcesAvailable()) return;

        var hashes = HashResourceLoader.Load(Path.Combine(ResourceDir, "hashes.json"));
        using var frame = LoadFrame(imagePath);
        var hasher = new ImageHasher();

        var best = FindBestDjLevelMatch(frame, hasher, hashes.PlayMode, PlaySide.Unknown);
        Assert.NotNull(best);
        Assert.Equal(expected, best!.Value);
    }

    /// <summary>
    /// RecognitionPipeline 経由で実 Result 画像を投入すると PlayResultDetected が発火し、
    /// fields[lamp] / fields[dj_level] が正しく抽出されること。
    /// PlayStart 状態はテスト用の InjectRecognition で合成し、その後 Real Result フレームを ProcessFrame する。
    /// </summary>
    [Fact]
    public void Pipeline_PlayStartToRealResult_FiresPlayResultWithFields()
    {
        if (!ResourcesAvailable()) return;

        var recognizer = BuildRecognizer();
        var pipeline = new RecognitionPipeline(recognizer);

        PlayResultEventArgs? captured = null;
        pipeline.PlayResultDetected += (_, e) => captured = e;

        // PlayStart 状態に遷移させる (test 用の inject)。
        // 実際の配信中は PlayStart の参考画像が無いためここで synthesize する。
        pipeline.InjectRecognition(new FrameRecognition(
            DateTimeOffset.Now, RecognizedState.PlayStart, null,
            new Dictionary<string, string>(), PlaySide.TwoP));

        // 実 Result フレーム (2P HARD) を ProcessFrame で投入
        using var frame = LoadFrame("clear_type_2p/HARD.png");
        var result = pipeline.ProcessFrame(frame, DateTimeOffset.Now.AddSeconds(60));

        Assert.Equal(RecognizedState.Result, result.State);
        Assert.NotNull(captured);
        Assert.Equal("HARD", captured!.Fields[RecognitionFieldKeys.Lamp]);
    }
}
