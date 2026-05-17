using System.Globalization;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.ColorAnalyzer;

// 指定パレット (difficulty / lamp) で ROI を分析するコンソールツール。
// Recognition.ColorBandDetector を直接使うので、実装と同じロジックで色判定結果を確認できる。
//
// 使い方:
//   ColorAnalyzer --image <path> --roi <x,y,w,h> [--palette difficulty|lamp]
//   ColorAnalyzer --image <path> --batch <csv> [--palette difficulty|lamp]
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

            var detector = options.Palette switch
            {
                "lamp" => ColorBandDetector.ForLamp(),
                _ => ColorBandDetector.ForDifficulty(),
            };

            var bytes = File.ReadAllBytes(options.ImagePath);
            using var frame = new ImageNormalizer().Normalize(bytes);

            if (options.RoiCsvPath is not null)
            {
                Console.WriteLine($"# palette={options.Palette}");
                Console.WriteLine("# name\troi\tdominant_label\tratio\tband_counts");
                foreach (var raw in File.ReadAllLines(options.RoiCsvPath))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    var parts = line.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length != 5) continue;
                    try
                    {
                        var roi = new Roi(
                            int.Parse(parts[1], CultureInfo.InvariantCulture),
                            int.Parse(parts[2], CultureInfo.InvariantCulture),
                            int.Parse(parts[3], CultureInfo.InvariantCulture),
                            int.Parse(parts[4], CultureInfo.InvariantCulture));
                        if (!roi.IsValid) continue;
                        AnalyzeAndPrint(parts[0], frame, roi, detector);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"スキップ ({ex.Message}): {line}");
                    }
                }
            }
            else if (options.Roi is not null)
            {
                Console.WriteLine($"# palette={options.Palette}");
                AnalyzeAndPrint("roi", frame, options.Roi, detector);
            }
            else
            {
                PrintUsage(); return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
            return 99;
        }
    }

    private static void AnalyzeAndPrint(string name, Mat frame, Roi roi, ColorBandDetector detector)
    {
        using var sub = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        var stats = detector.ComputeBandStats(sub);
        var label = stats.DominantLabel ?? "(none)";
        var ratio = stats.DominantRatio * 100.0;
        var bandStr = string.Join(",", stats.BandCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"));
        Console.WriteLine($"{name}\t[{roi.X},{roi.Y},{roi.Width},{roi.Height}]\t{label}\t{ratio:F1}%\t{bandStr}");
    }

    private static Options? ParseArgs(string[] args)
    {
        string? imagePath = null, roiArg = null, csvPath = null, palette = "difficulty";
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length: imagePath = args[++i]; break;
                case "--roi" when i + 1 < args.Length: roiArg = args[++i]; break;
                case "--batch" when i + 1 < args.Length: csvPath = args[++i]; break;
                case "--palette" when i + 1 < args.Length: palette = args[++i]; break;
            }
        }
        if (imagePath is null || (roiArg is null && csvPath is null)) return null;
        Roi? roi = roiArg is null ? null : ParseRoi(roiArg);
        return new Options(imagePath, roi, csvPath, palette);
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
  ColorAnalyzer --image <path> --roi <x,y,w,h> [--palette difficulty|lamp]
  ColorAnalyzer --image <path> --batch <csv-path> [--palette difficulty|lamp]

Recognition.ColorBandDetector を使い、ROI 内の支配色バンドを判定する。
""");
    }

    private sealed record Options(string ImagePath, Roi? Roi, string? RoiCsvPath, string Palette);
}
