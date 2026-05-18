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

            using var ocr = new TesseractOcrService(options.TessdataPath);
            if (!ocr.IsAvailable)
            {
                Console.Error.WriteLine($"Tesseract 初期化失敗。tessdata パス: {options.TessdataPath}");
                Console.Error.WriteLine("必要ファイル: eng.traineddata, jpn.traineddata");
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
        string? imagePath = null, roiArg = null, mode = "text", songsJson = null;
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length: imagePath = args[++i]; break;
                case "--roi" when i + 1 < args.Length: roiArg = args[++i]; break;
                case "--mode" when i + 1 < args.Length: mode = args[++i]; break;
                case "--tessdata" when i + 1 < args.Length: tessdataPath = args[++i]; break;
                case "--songs" when i + 1 < args.Length: songsJson = args[++i]; break;
            }
        }
        if (imagePath is null || roiArg is null) return null;
        return new Options(imagePath, ParseRoi(roiArg), mode, tessdataPath, songsJson);
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

    private sealed record Options(string ImagePath, Roi? Roi, string Mode, string TessdataPath, string? SongsJson);
}
