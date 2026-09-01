using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Tesseract;

namespace InfTimestamper.Core.Recognition;

public sealed class TesseractOcrService : IOcrService, IDisposable
{
    // 楽曲タイトルに日本語が混在するため jpn と eng の両対応。
    // 数字限定モードは Whitelist で 0-9 のみに制限するため、jpn が含まれていても影響しない
    public const string DefaultLanguage = "jpn+eng";
    public const string DigitWhitelist = "0123456789";

    private readonly ILogger<TesseractOcrService> _logger;
    private readonly TesseractEngine? _engine;
    private readonly object _gate = new();
    private bool _disposed;

    public TesseractOcrService(string tessdataPath)
        : this(tessdataPath, DefaultLanguage, NullLogger<TesseractOcrService>.Instance) { }

    public TesseractOcrService(string tessdataPath, string language, ILogger<TesseractOcrService> logger)
    {
        _logger = logger ?? NullLogger<TesseractOcrService>.Instance;

        if (!IsTessdataReady(tessdataPath, language))
        {
            _logger.LogWarning("tessdata が見つからないため OCR は無効化されます。パス: {Path}, 言語: {Lang}", tessdataPath, language);
            _engine = null;
            return;
        }

        try
        {
            _engine = new TesseractEngine(tessdataPath, language, EngineMode.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract エンジン初期化に失敗しました。OCR は無効化されます。");
            _engine = null;
        }
    }

    public bool IsAvailable => _engine is not null;

    // INFINITAS のタイトル / レベル / スコアはどれも 1 行表示のため SingleLine (PSM 7) で読む。
    // 既定の AutoOSD は複数行の文書を想定するので、改行が混入したり段落判定で壊れることがある。
    public OcrResult? RecognizeDigits(Mat roi) => RecognizeInternal(roi, DigitWhitelist, PageSegMode.SingleLine);

    public OcrResult? RecognizeText(Mat roi) => RecognizeInternal(roi, null, PageSegMode.SingleLine);

    private OcrResult? RecognizeInternal(Mat roi, string? whitelist, PageSegMode segMode)
    {
        if (_engine is null) return null;
        if (roi is null || roi.Empty()) return null;

        lock (_gate)
        {
            if (_disposed) return null;

            try
            {
                _engine.SetVariable("tessedit_char_whitelist", whitelist ?? string.Empty);
                _engine.DefaultPageSegMode = segMode;

                // 前処理: 2x 拡大 (小さい文字を読みやすく) → グレースケール変換。
                // INFINITAS のタイトル / スコアは装飾付き文字なので、二値化はせず階調を保つ
                using var processed = Preprocess(roi);
                var bytes = processed.ImEncode(".png");
                using var pix = Pix.LoadFromMemory(bytes);
                using var page = _engine.Process(pix);

                var text = page.GetText()?.Trim() ?? string.Empty;
                var confidence = page.GetMeanConfidence();
                return new OcrResult(text, confidence);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR 処理に失敗しました。");
                return null;
            }
        }
    }

    public const int BrightTextThreshold = 200;

    /// <summary>
    /// OCR 前処理:
    /// 1) 3x 拡大 (装飾フォントの細部を解像)
    /// 2) HSV の V (明度) チャネル抽出
    /// 3) しきい値 200 で二値化 (白系の文字本体のみ残す。グラデーションの暗い縁取りや背景は除去)
    /// 4) 色反転 (Tesseract は黒文字 / 白背景を前提に学習されているため)
    ///
    /// INFINITAS のタイトル / レベル / スコア / DJ LEVEL のいずれも明るい白系の文字なので、
    /// V チャネルの高輝度部分を抽出すれば背景の青グラデーション / アニメーション / 縁取りの暗色を排除できる。
    /// </summary>
    private static Mat Preprocess(Mat roi)
    {
        using var upscaled = new Mat();
        Cv2.Resize(roi, upscaled, new OpenCvSharp.Size(roi.Width * 3, roi.Height * 3),
            interpolation: InterpolationFlags.Cubic);

        using var hsv = new Mat();
        if (upscaled.Channels() == 1)
        {
            // 既にグレースケールならそのまま V 相当として使う
            upscaled.CopyTo(hsv);
        }
        else
        {
            Cv2.CvtColor(upscaled, hsv, ColorConversionCodes.BGR2HSV);
        }

        // V チャネル抽出
        using var value = new Mat();
        if (hsv.Channels() == 1)
        {
            hsv.CopyTo(value);
        }
        else
        {
            var channels = Cv2.Split(hsv);
            try
            {
                channels[2].CopyTo(value); // V = channel index 2
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
        }

        // 高輝度しきい値で二値化 (文字本体だけ残す)
        var binary = new Mat();
        Cv2.Threshold(value, binary, BrightTextThreshold, 255, ThresholdTypes.Binary);

        // 反転: 白文字 → 黒文字
        Cv2.BitwiseNot(binary, binary);
        return binary;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _engine?.Dispose();
        }
    }

    private static bool IsTessdataReady(string tessdataPath, string language)
    {
        if (string.IsNullOrWhiteSpace(tessdataPath)) return false;
        if (!Directory.Exists(tessdataPath)) return false;

        // jpn+eng のような複合言語指定は + で分割し、すべての言語ファイルが存在するかを確認
        foreach (var lang in language.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(tessdataPath, $"{lang.Trim()}.traineddata");
            if (!File.Exists(path)) return false;
        }
        return true;
    }
}
