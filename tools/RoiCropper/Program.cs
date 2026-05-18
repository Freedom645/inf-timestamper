using System.Globalization;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.RoiCropper;

// 指定 ROI を参考画像から切り抜いて PNG 保存する診断ツール。
// 1P/2P サイドの ROI 候補を視覚的に検証するために使う。
//
// 使い方:
//   RoiCropper --image <path> --roi <x,y,w,h> --out <png-path>
//   RoiCropper --image <path> --batch <csv> --out-dir <dir>
//
// CSV 行の例 (HashExtractor と同形式):
//   clear_type_1p, 89, 419, 49, 17
//   dj_level_1p, 58, 474, 144, 39
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

            var bytes = File.ReadAllBytes(options.ImagePath);
            using var frame = new ImageNormalizer().Normalize(bytes);

            if (options.RoiCsvPath is not null && options.OutDir is not null)
            {
                Directory.CreateDirectory(options.OutDir);
                foreach (var raw in File.ReadAllLines(options.RoiCsvPath))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    var parts = line.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length != 5)
                    {
                        Console.Error.WriteLine($"スキップ（フォーマット不正）: {line}");
                        continue;
                    }
                    try
                    {
                        var name = parts[0];
                        var roi = new Roi(
                            int.Parse(parts[1], CultureInfo.InvariantCulture),
                            int.Parse(parts[2], CultureInfo.InvariantCulture),
                            int.Parse(parts[3], CultureInfo.InvariantCulture),
                            int.Parse(parts[4], CultureInfo.InvariantCulture));
                        var outPath = Path.Combine(options.OutDir, $"{name}.png");
                        SaveCrop(frame, roi, outPath);
                        Console.WriteLine($"saved {outPath} [{roi.X},{roi.Y},{roi.Width},{roi.Height}]");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"スキップ ({ex.Message}): {line}");
                    }
                }
            }
            else if (options.Roi is not null && options.OutPath is not null)
            {
                SaveCrop(frame, options.Roi, options.OutPath);
                Console.WriteLine($"saved {options.OutPath} [{options.Roi.X},{options.Roi.Y},{options.Roi.Width},{options.Roi.Height}]");
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

    private static void SaveCrop(Mat frame, Roi roi, string outPath)
    {
        if (!roi.IsValid)
            throw new ArgumentException($"無効な ROI: [{roi.X},{roi.Y},{roi.Width},{roi.Height}]");
        if (roi.X + roi.Width > frame.Width || roi.Y + roi.Height > frame.Height)
            throw new ArgumentException(
                $"ROI が正規化フレーム ({frame.Width}x{frame.Height}) の範囲外: [{roi.X},{roi.Y},{roi.Width},{roi.Height}]");

        using var sub = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        Cv2.ImWrite(outPath, sub);
    }

    private static Options? ParseArgs(string[] args)
    {
        string? imagePath = null, roiArg = null, csvPath = null, outPath = null, outDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length: imagePath = args[++i]; break;
                case "--roi" when i + 1 < args.Length: roiArg = args[++i]; break;
                case "--batch" when i + 1 < args.Length: csvPath = args[++i]; break;
                case "--out" when i + 1 < args.Length: outPath = args[++i]; break;
                case "--out-dir" when i + 1 < args.Length: outDir = args[++i]; break;
            }
        }
        if (imagePath is null) return null;
        if (roiArg is null && csvPath is null) return null;
        Roi? roi = roiArg is null ? null : ParseRoi(roiArg);
        return new Options(imagePath, roi, csvPath, outPath, outDir);
    }

    private static Roi ParseRoi(string s)
    {
        var parts = s.Split(',');
        if (parts.Length != 4)
            throw new ArgumentException($"ROI は x,y,w,h 形式で指定してください: '{s}'");
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
  RoiCropper --image <path> --roi <x,y,w,h> --out <png-path>
  RoiCropper --image <path> --batch <csv> --out-dir <dir>

指定 ROI を画像から切り抜いて PNG 保存する診断ツール。
""");
    }

    private sealed record Options(
        string ImagePath, Roi? Roi, string? RoiCsvPath, string? OutPath, string? OutDir);
}
