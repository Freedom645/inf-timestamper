using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Tesseract;

namespace InfTimestamper.Core.Recognition;

public sealed class TesseractOcrService : IOcrService, IDisposable
{
    public const string DefaultLanguage = "eng";
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

    public OcrResult? RecognizeDigits(Mat roi) => RecognizeInternal(roi, DigitWhitelist);

    public OcrResult? RecognizeText(Mat roi) => RecognizeInternal(roi, null);

    private OcrResult? RecognizeInternal(Mat roi, string? whitelist)
    {
        if (_engine is null) return null;
        if (roi is null || roi.Empty()) return null;

        lock (_gate)
        {
            if (_disposed) return null;

            try
            {
                _engine.SetVariable("tessedit_char_whitelist", whitelist ?? string.Empty);

                var bytes = roi.ImEncode(".png");
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
        return File.Exists(Path.Combine(tessdataPath, $"{language}.traineddata"));
    }
}
