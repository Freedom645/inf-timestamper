using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using InfTimestamper.Core.Recognition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using AppOcrResult = InfTimestamper.Core.Recognition.OcrResult;
using WindowsOcrEngine = Windows.Media.Ocr.OcrEngine;

namespace InfTimestamper.Services;

/// <summary>
/// Windows 標準の OCR エンジン (Windows.Media.Ocr) を使った IOcrService 実装。
/// Tesseract に比べて装飾フォントやスクリーンキャプチャの文字に対して一般的に強い。
/// 必要 OS: Windows 10 May 2020 Update (build 19041) 以降。
/// </summary>
public sealed class WindowsMediaOcrService : IOcrService
{
    private readonly WindowsOcrEngine? _engine;
    private readonly ILogger<WindowsMediaOcrService> _logger;
    private readonly object _gate = new();

    public WindowsMediaOcrService(ILogger<WindowsMediaOcrService>? logger = null)
    {
        _logger = logger ?? NullLogger<WindowsMediaOcrService>.Instance;

        // ユーザの言語プロファイルから OCR エンジンを生成 (jpn / eng が入っていれば両対応)。
        // 失敗時は en-US でフォールバック
        try
        {
            _engine = WindowsOcrEngine.TryCreateFromUserProfileLanguages();
            if (_engine is null)
            {
                _engine = WindowsOcrEngine.TryCreateFromLanguage(new Language("en-US"));
                _logger.LogInformation("Windows.Media.Ocr: en-US でフォールバック初期化");
            }
            else
            {
                _logger.LogInformation("Windows.Media.Ocr: ユーザ言語プロファイルで初期化 (RecognizerLanguage={Lang})", _engine.RecognizerLanguage.LanguageTag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Windows.Media.Ocr の初期化に失敗。OCR は無効化されます。");
            _engine = null;
        }
    }

    public bool IsAvailable => _engine is not null;

    public AppOcrResult? RecognizeText(Mat roi) => Recognize(roi, digitsOnly: false);

    /// <summary>
    /// Windows.Media.Ocr はホワイトリストを持たないので、認識後にスペース区切りで数字だけ取り出す。
    /// "55 4" のような余計な区切りも、結合した数字並びを採用することで吸収する。
    /// </summary>
    public AppOcrResult? RecognizeDigits(Mat roi)
    {
        var result = Recognize(roi, digitsOnly: false);
        if (result is null || string.IsNullOrEmpty(result.Text)) return result;

        var sb = new StringBuilder(result.Text.Length);
        foreach (var ch in result.Text)
        {
            if (ch >= '0' && ch <= '9') sb.Append(ch);
        }
        return new AppOcrResult(sb.ToString(), result.MeanConfidence);
    }

    private AppOcrResult? Recognize(Mat roi, bool digitsOnly)
    {
        if (_engine is null) return null;
        if (roi is null || roi.Empty()) return null;

        lock (_gate)
        {
            try
            {
                // Mat → PNG → SoftwareBitmap → OCR
                var pngBytes = roi.ImEncode(".png");
                return RecognizeAsync(pngBytes).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Windows.Media.Ocr 処理に失敗しました。");
                return null;
            }
        }
    }

    private async Task<AppOcrResult?> RecognizeAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var ocr = await _engine!.RecognizeAsync(bitmap);
        if (ocr is null) return new AppOcrResult(string.Empty, 0f);

        var text = ocr.Text ?? string.Empty;

        // Windows.Media.Ocr の OcrResult には MeanConfidence が無い。Words の信頼度を平均する。
        double meanConfidence = 0;
        int wordCount = 0;
        foreach (var line in ocr.Lines)
        foreach (var word in line.Words)
        {
            wordCount++;
            // Windows OCR の Word には Confidence が無い (API 上)。フォールバックで 1.0 を採用。
            // 必要なら Lines.Count や Words.Count を使った別の指標を入れる
            meanConfidence += 1.0;
        }
        var confidence = wordCount > 0 ? (float)(meanConfidence / wordCount) : 0f;

        return new AppOcrResult(text.Trim(), confidence);
    }
}
