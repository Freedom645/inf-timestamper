using System.Globalization;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.OcrTester;

// 指定画像の指定 ROI に対して Tesseract OCR を実行し、抽出結果を表示する診断ツール。
// 実プレイで保存されたデバッグフレームと rois.json の組み合わせで title / level / score の
// OCR 精度を素早く確認するために使う。
//
// 使い方:
//   OcrTester --image <path> --roi <x,y,w,h> [--mode text|digits] [--tessdata <path>]
internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options is null) { PrintUsage(); return 1; }

            if (!File.Exists(options.ImagePath))
            {
                Console.Error.WriteLine($"画像が見つかりません: {options.ImagePath}");
                return 2;
            }

            using var ocr = new TesseractOcrService(options.TessdataPath, options.Language,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TesseractOcrService>.Instance);
            if (!ocr.IsAvailable)
            {
                Console.Error.WriteLine($"Tesseract 初期化失敗。tessdata パス: {options.TessdataPath} 言語: {options.Language}");
                return 3;
            }

            var bytes = File.ReadAllBytes(options.ImagePath);
            using var frame = new ImageNormalizer().Normalize(bytes);

            if (options.Roi is null)
            {
                Console.Error.WriteLine("ROI が指定されていません。");
                return 1;
            }

            using var sub = new Mat(frame, new Rect(options.Roi.X, options.Roi.Y, options.Roi.Width, options.Roi.Height));

            // デバッグ: 前処理パイプラインを TesseractOcrService と同じ手順で再現して保存。
            // 何が Tesseract に渡っているかを目視で確認する。
            if (options.DumpPath is not null)
            {
                using var upscaled = new Mat();
                Cv2.Resize(sub, upscaled, new OpenCvSharp.Size(sub.Width * 3, sub.Height * 3),
                    interpolation: InterpolationFlags.Cubic);
                using var hsv = new Mat();
                Cv2.CvtColor(upscaled, hsv, ColorConversionCodes.BGR2HSV);
                var channels = Cv2.Split(hsv);
                using var value = channels[2];
                channels[0].Dispose();
                channels[1].Dispose();
                using var binary = new Mat();
                Cv2.Threshold(value, binary, TesseractOcrService.BrightTextThreshold, 255, ThresholdTypes.Binary);
                Cv2.BitwiseNot(binary, binary);
                Cv2.ImWrite(options.DumpPath, binary);
                Console.WriteLine($"preprocessed image saved to: {options.DumpPath}");
            }

            var result = options.Mode == "digits"
                ? ocr.RecognizeDigits(sub)
                : ocr.RecognizeText(sub);

            if (result is null)
            {
                Console.WriteLine("(OCR が null を返しました)");
                return 0;
            }

            Console.WriteLine($"text: \"{result.Text}\"");
            Console.WriteLine($"confidence: {result.MeanConfidence:F2}");

            if (options.SongsJson is not null && options.Mode != "digits")
            {
                if (File.Exists(options.SongsJson))
                {
                    var repo = SongRepository.LoadFromFile(options.SongsJson);
                    var matcher = new SongTitleMatcher(repo);
                    var match = matcher.Match(result.Text);
                    Console.WriteLine($"fuzzy: {match.Kind} title=\"{match.Title}\" distance={match.Distance?.ToString() ?? "n/a"}");
                }
                else
                {
                    Console.Error.WriteLine($"songs.json が見つかりません: {options.SongsJson}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
            return 99;
        }
    }

    private static Options? ParseArgs(string[] args)
    {
        string? imagePath = null, roiArg = null, mode = "text", songsJson = null, dumpPath = null;
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        string language = TesseractOcrService.DefaultLanguage;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length: imagePath = args[++i]; break;
                case "--roi" when i + 1 < args.Length: roiArg = args[++i]; break;
                case "--mode" when i + 1 < args.Length: mode = args[++i]; break;
                case "--tessdata" when i + 1 < args.Length: tessdataPath = args[++i]; break;
                case "--songs" when i + 1 < args.Length: songsJson = args[++i]; break;
                case "--dump" when i + 1 < args.Length: dumpPath = args[++i]; break;
                case "--lang" when i + 1 < args.Length: language = args[++i]; break;
            }
        }
        if (imagePath is null || roiArg is null) return null;
        return new Options(imagePath, ParseRoi(roiArg), mode, tessdataPath, songsJson, dumpPath, language);
    }

    private static Roi ParseRoi(string s)
    {
        var parts = s.Split(',');
        return new Roi(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture),
            int.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
Usage:
  OcrTester --image <path> --roi <x,y,w,h> [--mode text|digits] [--tessdata <path>]
""");
    }

    private sealed record Options(string ImagePath, Roi? Roi, string Mode, string TessdataPath, string? SongsJson, string? DumpPath, string Language);
}
