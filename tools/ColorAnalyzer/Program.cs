using System.Globalization;
using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.ColorAnalyzer;

// 入力画像内の ROI に対して、HSV ベースの色分析を行う。
//
// 出力:
//   - HSV 平均値 (H, S, V)
//   - 高彩度ピクセル（S>=80 && V>=80）の HSV ヒストグラムから最頻 H bin
//   - 各「難易度色バンド」（緑/青/黄/赤/紫）に該当するピクセル割合
//
// 使い方:
//   ColorAnalyzer --image <path> --roi <x,y,w,h>
//   ColorAnalyzer --image <path> --batch <csv-path>
internal static class Program
{
    // 難易度色の HSV (OpenCV: H=0-179, S/V=0-255) おおまかな範囲
    // INFINITAS の表示色のおおよその位置。実測で調整する想定
    private static readonly (string Name, int HMin, int HMax)[] DifficultyColorBands =
    {
        ("Red(A)",     0,  10),   // 赤 = ANOTHER
        ("Yellow(H)", 20,  35),   // 黄 = HYPER
        ("Green(B)",  40,  85),   // 緑 = BEGINNER
        ("Blue(N)",   90, 135),   // 青 = NORMAL
        ("Purple(L)",140, 165),   // 紫 = LEGGENDARIA
        ("Red2(A)",  170, 179),   // 赤の H=0 折り返し側
    };

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
            var normalizer = new ImageNormalizer();
            using var frame = normalizer.Normalize(bytes);

            if (options.RoiCsvPath is not null)
            {
                Console.WriteLine("# name\troi\tHavg\tSavg\tVavg\tHmode\tdominant_band");
                foreach (var raw in File.ReadAllLines(options.RoiCsvPath))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    var parts = line.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length != 5) continue;
                    var name = parts[0];
                    try
                    {
                        var roi = new Roi(
                            int.Parse(parts[1], CultureInfo.InvariantCulture),
                            int.Parse(parts[2], CultureInfo.InvariantCulture),
                            int.Parse(parts[3], CultureInfo.InvariantCulture),
                            int.Parse(parts[4], CultureInfo.InvariantCulture));
                        if (!roi.IsValid) continue;
                        AnalyzeAndPrint(name, frame, roi);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"スキップ ({ex.Message}): {line}");
                    }
                }
            }
            else if (options.Roi is not null)
            {
                AnalyzeAndPrint("roi", frame, options.Roi);
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

    private static void AnalyzeAndPrint(string name, Mat frame, Roi roi)
    {
        using var sub = new Mat(frame, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
        using var hsv = new Mat();
        Cv2.CvtColor(sub, hsv, ColorConversionCodes.BGR2HSV);

        var hsvMean = Cv2.Mean(hsv);

        // 高彩度・高明度なピクセルだけ拾って H のヒストグラムを作る
        var histH = new int[180];
        int sampled = 0;
        var bandCounts = new int[DifficultyColorBands.Length];

        for (int y = 0; y < hsv.Rows; y++)
        {
            for (int x = 0; x < hsv.Cols; x++)
            {
                var p = hsv.At<Vec3b>(y, x);
                int h = p.Item0; int s = p.Item1; int v = p.Item2;
                if (s < 80 || v < 80) continue;
                histH[h]++;
                sampled++;
                for (int i = 0; i < DifficultyColorBands.Length; i++)
                {
                    var (_, lo, hi) = DifficultyColorBands[i];
                    if (h >= lo && h <= hi) bandCounts[i]++;
                }
            }
        }

        // 最頻 H
        int hMode = -1;
        int hModeCount = 0;
        for (int i = 0; i < 180; i++)
        {
            if (histH[i] > hModeCount) { hModeCount = histH[i]; hMode = i; }
        }

        // 支配バンド（最大票）
        int dominantBand = -1;
        int dominantCount = 0;
        for (int i = 0; i < bandCounts.Length; i++)
        {
            if (bandCounts[i] > dominantCount) { dominantCount = bandCounts[i]; dominantBand = i; }
        }

        var dominantName = dominantBand >= 0 ? DifficultyColorBands[dominantBand].Name : "(none)";
        var dominantPct = sampled > 0 ? (100.0 * dominantCount / sampled) : 0.0;
        var sampledPct = (100.0 * sampled / (hsv.Rows * hsv.Cols));

        Console.WriteLine(
            $"{name}\t[{roi.X},{roi.Y},{roi.Width},{roi.Height}]" +
            $"\tH={hsvMean.Val0:F1}\tS={hsvMean.Val1:F1}\tV={hsvMean.Val2:F1}" +
            $"\tHmode={hMode}({hModeCount}px,sat={sampledPct:F0}%)" +
            $"\t{dominantName}({dominantPct:F1}%)");
    }

    private static Options? ParseArgs(string[] args)
    {
        string? imagePath = null, roiArg = null, csvPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--image" when i + 1 < args.Length: imagePath = args[++i]; break;
                case "--roi" when i + 1 < args.Length: roiArg = args[++i]; break;
                case "--batch" when i + 1 < args.Length: csvPath = args[++i]; break;
            }
        }
        if (imagePath is null || (roiArg is null && csvPath is null)) return null;
        Roi? roi = roiArg is null ? null : ParseRoi(roiArg);
        return new Options(imagePath, roi, csvPath);
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
  ColorAnalyzer --image <path> --roi <x,y,w,h>
  ColorAnalyzer --image <path> --batch <csv-path>

ROI 内の HSV ベース支配色を分析し、難易度色バンド (Red/Yellow/Green/Blue/Purple)
の中で最も該当ピクセルが多いものを表示する。
""");
    }

    private sealed record Options(string ImagePath, Roi? Roi, string? RoiCsvPath);
}
