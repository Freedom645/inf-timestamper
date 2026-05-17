using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public sealed class FrameRecognizer
{
    private readonly ImageNormalizer _normalizer;
    private readonly IImageHasher _hasher;
    private readonly IOcrService _ocr;
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
        ILogger<FrameRecognizer>? logger = null)
    {
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
        _rois = rois ?? throw new ArgumentNullException(nameof(rois));
        _songMatcher = songMatcher;
        _normalizer = normalizer ?? new ImageNormalizer();
        _logger = logger ?? NullLogger<FrameRecognizer>.Instance;
    }

    public FrameRecognition Recognize(ObsScreenshot screenshot)
    {
        if (screenshot is null) throw new ArgumentNullException(nameof(screenshot));

        using var frame = _normalizer.Normalize(screenshot.PngBytes);
        return RecognizeFrame(frame, screenshot.CapturedAt);
    }

    public FrameRecognition RecognizeFrame(Mat normalizedFrame, DateTimeOffset capturedAt)
    {
        if (normalizedFrame is null || normalizedFrame.Empty())
            throw new ArgumentException("空のフレームが渡されました。", nameof(normalizedFrame));

        var (state, stateMatch) = DetectState(normalizedFrame);
        var fields = new Dictionary<string, string>();

        switch (state)
        {
            case RecognizedState.SongSelect:
            case RecognizedState.PlayStart:
                ExtractSelectionFields(normalizedFrame, fields);
                break;
            case RecognizedState.Result:
                ExtractResultFields(normalizedFrame, fields);
                break;
        }

        return new FrameRecognition(capturedAt, state, stateMatch, fields);
    }

    private (RecognizedState State, HashMatchResult? Match) DetectState(Mat frame)
    {
        if (_hashes.States.Count == 0) return (RecognizedState.Unknown, null);

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

        return (bestState, bestMatch);
    }

    private void ExtractSelectionFields(Mat frame, Dictionary<string, string> fields)
    {
        var diffMatch = MatchIcons(frame, _hashes.Difficulty);
        if (diffMatch is not null)
        {
            fields[RecognitionFieldKeys.DiffShort] = diffMatch.Value;
            var longName = DifficultyShortToLong(diffMatch.Value);
            if (!string.IsNullOrEmpty(longName))
                fields[RecognitionFieldKeys.DiffLong] = longName;
        }

        ApplyOcrDigit(frame, RecognitionFieldKeys.Level, fields);
        ApplyTitleOcr(frame, fields);
    }

    private void ExtractResultFields(Mat frame, Dictionary<string, string> fields)
    {
        var dj = MatchIcons(frame, _hashes.DjLevel);
        if (dj is not null) fields[RecognitionFieldKeys.DjLevel] = dj.Value;

        var lamp = MatchIcons(frame, _hashes.Lamp);
        if (lamp is not null) fields[RecognitionFieldKeys.Lamp] = lamp.Value;

        ApplyOcrDigit(frame, RecognitionFieldKeys.MissCount, fields);
        ApplyOcrDigit(frame, RecognitionFieldKeys.ExScore, fields);
    }

    private IconHashEntry? MatchIcons(Mat frame, IReadOnlyList<IconHashEntry> candidates)
    {
        if (candidates.Count == 0) return null;

        IconHashEntry? best = null;
        int bestDistance = int.MaxValue;

        foreach (var entry in candidates)
        {
            if (!entry.Roi.IsValid) continue;
            if (!IsRoiInside(entry.Roi, frame)) continue;

            using var roi = SubMat(frame, entry.Roi);
            var hash = _hasher.ComputeAverageHash(roi);
            var distance = ImageHasher.HammingDistance(hash, entry.Ahash);
            if (distance > entry.Threshold) continue;

            if (distance < bestDistance)
            {
                best = entry;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void ApplyOcrDigit(Mat frame, string key, Dictionary<string, string> fields)
    {
        if (!_ocr.IsAvailable) return;
        if (!_rois.TryGet(key, out var roi) || !roi.IsValid) return;
        if (!IsRoiInside(roi, frame)) return;

        using var region = SubMat(frame, roi);
        var result = _ocr.RecognizeDigits(region);
        if (result is null || string.IsNullOrEmpty(result.Text)) return;

        fields[key] = result.Text;
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
