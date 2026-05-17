using System.Globalization;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.HashExtractor;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options is null)
            {
                PrintUsage();
                return 1;
            }

            if (!File.Exists(options.ImagePath))
            {
                Console.Error.WriteLine($"エラー: 画像ファイルが見つかりません: {options.ImagePath}");
                return 2;
            }

            var bytes = File.ReadAllBytes(options.ImagePath);
            var normalizer = new ImageNormalizer();
            using var frame = normalizer.Normalize(bytes);
            var hasher = new ImageHasher();

            if (options.RoiCsvPath is not null)
            {
                RunBatch(frame, hasher, options.RoiCsvPath);
            }
            else if (options.Roi is not null)
            {
                var hash = ExtractHash(frame, options.Roi, hasher);
                Console.WriteLine(FormatLine("hash", options.Roi, hash));
            }
            else
            {
                PrintUsage();
                return 1;
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
        string? imagePath = null;
        string? roiArg = null;
        string? csvPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length:
                    imagePath = args[++i];
                    break;
                case "--roi" when i + 1 < args.Length:
                    roiArg = args[++i];
                    break;
                case "--batch" when i + 1 < args.Length:
                    csvPath = args[++i];
                    break;
            }
        }

        if (imagePath is null) return null;
        if (roiArg is null && csvPath is null) return null;

        Roi? roi = roiArg is null ? null : ParseRoi(roiArg);
        return new Options(imagePath, roi, csvPath);
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

    private static void RunBatch(Mat frame, ImageHasher hasher, string csvPath)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"バッチ定義ファイルが見つかりません: {csvPath}");

        Console.WriteLine("# name\troi\tahash");
        foreach (var raw in File.ReadAllLines(csvPath))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            // 形式: name,x,y,w,h
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 5)
            {
                Console.Error.WriteLine($"スキップ（フォーマット不正）: {line}");
                continue;
            }

            var name = parts[0];
            try
            {
                var roi = new Roi(
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    int.Parse(parts[3], CultureInfo.InvariantCulture),
                    int.Parse(parts[4], CultureInfo.InvariantCulture));
                var hash = ExtractHash(frame, roi, hasher);
                Console.WriteLine(FormatLine(name, roi, hash));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"スキップ（{ex.Message}）: {line}");
            }
        }
    }

    private static ulong ExtractHash(Mat frame, Roi roi, ImageHasher hasher)
    {
        if (!roi.IsValid)
            throw new ArgumentException($"無効な ROI: [{roi.X},{roi.Y},{roi.Width},{roi.Height}]");
        if (roi.X + roi.Width > frame.Width || roi.Y + roi.Height > frame.Height)
            throw new ArgumentException(
                $"ROI が正規化後フレーム ({frame.Width}x{frame.Height}) の範囲外: [{roi.X},{roi.Y},{roi.Width},{roi.Height}]");

        using var sub = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        return hasher.ComputeAverageHash(sub);
    }

    private static string FormatLine(string name, Roi roi, ulong hash)
        => $"{name}\t[{roi.X},{roi.Y},{roi.Width},{roi.Height}]\t0x{hash:x16}";

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
Usage:
  HashExtractor --image <path> --roi <x,y,w,h>
  HashExtractor --image <path> --batch <csv-path>

Options:
  --image <path>       1920x1080 に正規化される入力画像（PNG / JPEG など）
  --roi   <x,y,w,h>    単一 ROI の aHash を計算
  --batch <csv-path>   バッチ計算（CSV: name,x,y,w,h）

CSV 行の例:
  song_select_1p, 1700, 30, 100, 60
  difficulty_SPA, 720, 540, 80, 30

# で始まる行と空行はコメントとして無視。

出力フォーマット (TSV):
  <name>\t[<x>,<y>,<w>,<h>]\t0x<hex16>
""");
    }

    private sealed record Options(string ImagePath, Roi? Roi, string? RoiCsvPath);
}
