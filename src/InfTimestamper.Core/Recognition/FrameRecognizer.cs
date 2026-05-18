using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public sealed class FrameRecognizer
{
    /// <summary>
    /// 色判定で SP/DP が分からない場合の既定プレイモードプレフィックス。
    /// INFINITAS のほとんどのプレイは SP のため、SP を既定とする。
    /// </summary>
    public const string DefaultPlayModePrefix = "SP";

    /// <summary>
    /// FAILED 判定で背景 ROI の赤ピクセル比率がこの値以上なら HARD → FAILED に書き換える。
    /// 実画像では FAILED が 100% 赤、それ以外 (HARD 含む) は最大 12% 程度のため、
    /// 0.7 で十分なマージンがある。
    /// </summary>
    internal const double FailedBackgroundRedRatio = 0.7;

    private readonly ImageNormalizer _normalizer;
    private readonly IImageHasher _hasher;
    private readonly IOcrService _ocr;
    private readonly ColorBandDetector _difficultyColorDetector;
    private readonly ColorBandDetector _lampColorDetector;
    private readonly SongTitleMatcher? _songMatcher;
    private readonly HashResource _hashes;
    private readonly RoiResource _rois;
    private readonly ILogger<FrameRecognizer> _logger;

    public FrameRecognizer(
        IImageHasher hasher,
        IOcrService ocr,
        HashResource hashes,
        RoiResource rois,
        SongTitleMatcher? songMatcher = null,
        ImageNormalizer? normalizer = null,
        ColorBandDetector? difficultyColorDetector = null,
        ColorBandDetector? lampColorDetector = null,
        ILogger<FrameRecognizer>? logger = null)
    {
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
        _rois = rois ?? throw new ArgumentNullException(nameof(rois));
        _songMatcher = songMatcher;
        _normalizer = normalizer ?? new ImageNormalizer();
        _difficultyColorDetector = difficultyColorDetector ?? ColorBandDetector.ForDifficulty();
        _lampColorDetector = lampColorDetector ?? ColorBandDetector.ForLamp();
        _logger = logger ?? NullLogger<FrameRecognizer>.Instance;
    }

    public FrameRecognition Recognize(ObsScreenshot screenshot, PlaySide hintSide = PlaySide.Unknown)
    {
        if (screenshot is null) throw new ArgumentNullException(nameof(screenshot));

        using var frame = _normalizer.Normalize(screenshot.PngBytes);
        return RecognizeFrame(frame, screenshot.CapturedAt, hintSide);
    }

    public FrameRecognition RecognizeFrame(
        Mat normalizedFrame,
        DateTimeOffset capturedAt,
        PlaySide hintSide = PlaySide.Unknown)
    {
        if (normalizedFrame is null || normalizedFrame.Empty())
            throw new ArgumentException("空のフレームが渡されました。", nameof(normalizedFrame));

        var (state, stateMatch, detectedSide) = DetectState(normalizedFrame);
        var effectiveSide = detectedSide != PlaySide.Unknown ? detectedSide : hintSide;
        var fields = new Dictionary<string, string>();

        switch (state)
        {
            case RecognizedState.SongSelect:
            case RecognizedState.PlayStart:
                ExtractSelectionFields(normalizedFrame, fields, effectiveSide);
                break;
            case RecognizedState.Result:
                ExtractResultFields(normalizedFrame, fields, effectiveSide);
                break;
        }

        return new FrameRecognition(capturedAt, state, stateMatch, fields, detectedSide);
    }

    private (RecognizedState State, HashMatchResult? Match, PlaySide Side) DetectState(Mat frame)
    {
        if (_hashes.States.Count == 0)
            return (RecognizedState.Unknown, null, PlaySide.Unknown);

        RecognizedState bestState = RecognizedState.Unknown;
        HashMatchResult? bestMatch = null;

        foreach (var (stateName, entries) in _hashes.States)
        {
            foreach (var entry in entries)
            {
                if (!entry.Roi.IsValid) continue;
                if (!IsRoiInside(entry.Roi, frame)) continue;

                using var roi = SubMat(frame, entry.Roi);
                var hash = _hasher.ComputeAverageHash(roi);
                var distance = ImageHasher.HammingDistance(hash, entry.Ahash);
                if (distance > entry.Threshold) continue;

                if (bestMatch is null || distance < bestMatch.Distance)
                {
                    bestMatch = new HashMatchResult($"{stateName}/{entry.Name}", distance);
                    bestState = RecognizedStateNames.FromString(stateName);
                }
            }
        }

        var side = PlaySides.FromStateName(bestMatch?.Name);
        return (bestState, bestMatch, side);
    }

    private void ExtractSelectionFields(Mat frame, Dictionary<string, string> fields, PlaySide side)
    {
        // SP / DP プレフィックスを aHash/pHash 照合で判定。検出できなければ既定 (SP) にフォールバック。
        var playModePrefix = DetectPlayMode(frame) ?? DefaultPlayModePrefix;

        // 1) 優先: 色判定（rois.json の "difficulty_color" ROI を使う）
        //    HSV 支配色で B/N/H/A/L を 1 文字判定。
        if (TryDetectDifficultyByColor(frame, out var colorLetter))
        {
            var diffShort = playModePrefix + colorLetter;
            fields[RecognitionFieldKeys.DiffShort] = diffShort;
            var longName = DifficultyShortToLong(diffShort);
            if (!string.IsNullOrEmpty(longName))
                fields[RecognitionFieldKeys.DiffLong] = longName;
        }
        else
        {
            // 2) フォールバック: 既存の aHash 照合（hashes.json の "difficulty" セクション）
            var diffMatch = MatchIcons(frame, _hashes.Difficulty, side);
            if (diffMatch is not null)
            {
                fields[RecognitionFieldKeys.DiffShort] = diffMatch.Value;
                var longName = DifficultyShortToLong(diffMatch.Value);
                if (!string.IsNullOrEmpty(longName))
                    fields[RecognitionFieldKeys.DiffLong] = longName;
            }
        }

        // SongSelect / PlayStart の OCR ROI は 1P/2P 共通（楽曲名は中央付近、レベルもプレイサイドに依存しない）
        ApplyOcrDigit(frame, RecognitionFieldKeys.Level, RecognitionFieldKeys.Level, fields);
        ApplyTitleOcr(frame, fields);
    }

    /// <summary>
    /// hashes.json の "play_mode" セクションから SP / DP を検出する。
    /// マッチした entry の Value (例: "SP" / "DP") を返す。マッチなしなら null。
    /// </summary>
    private string? DetectPlayMode(Mat frame)
    {
        var match = MatchIcons(frame, _hashes.PlayMode);
        return match?.Value;
    }

    private bool TryDetectDifficultyByColor(Mat frame, out string colorLetter)
    {
        colorLetter = string.Empty;
        if (!_rois.TryGet(RecognitionRoiKeys.DifficultyColor, out var roi)) return false;
        if (!roi.IsValid) return false;
        if (!IsRoiInside(roi, frame)) return false;

        using var sub = SubMat(frame, roi);
        var detected = _difficultyColorDetector.Detect(sub);
        if (string.IsNullOrEmpty(detected)) return false;

        colorLetter = detected;
        return true;
    }

    /// <summary>
    /// FAILED 時は画面全体が赤いオーバーレイになる性質を利用して HARD と区別する。
    /// 背景 ROI 内で lamp パレットの HARD (赤) バンドが <see cref="FailedBackgroundRedRatio"/> 以上を占めるとき true。
    /// </summary>
    private bool IsFailedBackground(Mat frame)
    {
        if (!_rois.TryGet(RecognitionRoiKeys.FailedBackground, out var roi)) return false;
        if (!roi.IsValid) return false;
        if (!IsRoiInside(roi, frame)) return false;

        using var sub = SubMat(frame, roi);
        var stats = _lampColorDetector.ComputeBandStats(sub);
        if (stats.BandCounts.Count == 0) return false;

        var totalMatched = stats.BandCounts.Values.Sum();
        if (totalMatched == 0) return false;

        var hardCount = stats.BandCounts.TryGetValue("HARD", out var c) ? c : 0;
        return (double)hardCount / totalMatched >= FailedBackgroundRedRatio;
    }

    private bool TryDetectLampByColor(Mat frame, PlaySide side, out string lampLabel)
    {
        lampLabel = string.Empty;
        var roiKey = RecognitionRoiKeys.LampColor(side);
        if (!_rois.TryGet(roiKey, out var roi)) return false;
        if (!roi.IsValid) return false;
        if (!IsRoiInside(roi, frame)) return false;

        using var sub = SubMat(frame, roi);
        var detected = _lampColorDetector.Detect(sub);
        if (string.IsNullOrEmpty(detected)) return false;

        lampLabel = detected;
        return true;
    }

    private void ExtractResultFields(Mat frame, Dictionary<string, string> fields, PlaySide side)
    {
        var dj = MatchIcons(frame, _hashes.DjLevel, side);
        if (dj is not null) fields[RecognitionFieldKeys.DjLevel] = dj.Value;

        // 1) ランプは色判定を優先（rois.json の "lamp_color_1p"/"lamp_color_2p" ROI）
        //    HARD と FAILED は同色（赤）のため、Red バンドは HARD を返す。
        //    HARD と判定された場合のみ、FAILED 判定 (background 赤さの確認) を実行。
        if (TryDetectLampByColor(frame, side, out var lampLabel))
        {
            if (lampLabel == "HARD" && IsFailedBackground(frame))
                lampLabel = "FAILED";
            fields[RecognitionFieldKeys.Lamp] = lampLabel;
        }
        else
        {
            // 2) フォールバック: aHash 照合
            var lamp = MatchIcons(frame, _hashes.Lamp, side);
            if (lamp is not null) fields[RecognitionFieldKeys.Lamp] = lamp.Value;
        }

        // OCR ROI はサイド別: "miss_count_1p" / "miss_count_2p"（同 ex_score）
        ApplyOcrDigit(frame, RecognitionFieldKeys.MissCount, RecognitionRoiKeys.WithSide(RecognitionFieldKeys.MissCount, side), fields);
        ApplyOcrDigit(frame, RecognitionFieldKeys.ExScore, RecognitionRoiKeys.WithSide(RecognitionFieldKeys.ExScore, side), fields);
    }

    private IconHashEntry? MatchIcons(Mat frame, IReadOnlyList<IconHashEntry> candidates, PlaySide side = PlaySide.Unknown)
    {
        if (candidates.Count == 0) return null;

        IconHashEntry? best = null;
        int bestDistance = int.MaxValue;

        foreach (var entry in candidates)
        {
            // サイド指定があるエントリは current side と一致するものだけ照合する。
            // entry.Side == Unknown のエントリは全サイドで使用可。
            if (entry.Side != PlaySide.Unknown && side != PlaySide.Unknown && entry.Side != side) continue;

            if (!entry.Roi.IsValid) continue;
            if (!IsRoiInside(entry.Roi, frame)) continue;

            using var roi = SubMat(frame, entry.Roi);
            var hash = entry.Algo switch
            {
                HashAlgorithm.Perceptual => _hasher.ComputePerceptualHash(roi),
                _ => _hasher.ComputeAverageHash(roi),
            };
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

    private void ApplyOcrDigit(Mat frame, string fieldKey, string roiKey, Dictionary<string, string> fields)
    {
        if (!_ocr.IsAvailable) return;
        if (!_rois.TryGet(roiKey, out var roi) || !roi.IsValid) return;
        if (!IsRoiInside(roi, frame)) return;

        using var region = SubMat(frame, roi);
        var result = _ocr.RecognizeDigits(region);
        if (result is null || string.IsNullOrEmpty(result.Text)) return;

        fields[fieldKey] = result.Text;
    }

    private void ApplyTitleOcr(Mat frame, Dictionary<string, string> fields)
    {
        if (!_ocr.IsAvailable) return;
        if (!_rois.TryGet(RecognitionFieldKeys.Title, out var roi) || !roi.IsValid) return;
        if (!IsRoiInside(roi, frame)) return;

        using var region = SubMat(frame, roi);
        var ocrResult = _ocr.RecognizeText(region);
        if (ocrResult is null || string.IsNullOrEmpty(ocrResult.Text)) return;

        if (_songMatcher is null)
        {
            fields[RecognitionFieldKeys.Title] = ocrResult.Text;
            return;
        }

        var match = _songMatcher.Match(ocrResult.Text);
        // Confirmed / Candidate のとき DB の正規タイトル、Unmatched なら生 OCR
        fields[RecognitionFieldKeys.Title] = match.Title;
    }

    private static Mat SubMat(Mat src, Roi roi)
        => new(src, new Rect(roi.X, roi.Y, roi.Width, roi.Height));

    private static bool IsRoiInside(Roi roi, Mat frame)
        => roi.X >= 0 && roi.Y >= 0
           && roi.X + roi.Width <= frame.Width
           && roi.Y + roi.Height <= frame.Height;

    internal static string DifficultyShortToLong(string diffShort)
    {
        if (string.IsNullOrEmpty(diffShort)) return string.Empty;
        var last = diffShort[^1];
        return last switch
        {
            'B' => "BEGINNER",
            'N' => "NORMAL",
            'H' => "HYPER",
            'A' => "ANOTHER",
            'L' => "LEGGENDARIA",
            _ => string.Empty,
        };
    }
}
