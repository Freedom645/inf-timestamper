using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public sealed class ImageNormalizer
{
    public const int TargetWidth = 1920;
    public const int TargetHeight = 1080;

    public Mat Normalize(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
            throw new InvalidDataException("空の画像バイト列が渡されました。");

        var src = Cv2.ImDecode(pngBytes, ImreadModes.Color);
        if (src.Empty())
            throw new InvalidDataException("画像をデコードできません。");

        try
        {
            return NormalizeInternal(src);
        }
        finally
        {
            src.Dispose();
        }
    }

    public Mat NormalizeFromMat(Mat src)
    {
        if (src is null || src.Empty())
            throw new InvalidDataException("空の Mat が渡されました。");
        return NormalizeInternal(src);
    }

    private static Mat NormalizeInternal(Mat src)
    {
        var w = src.Width;
        var h = src.Height;

        if (w == TargetWidth && h == TargetHeight)
            return src.Clone();

        // 高さは合っているが横が欠けている入力（例: 1360x1080）
        if (h == TargetHeight && w < TargetWidth)
            return PadToTarget(src, padTop: 0, padBottom: 0);

        // それ以外はアスペクト比を保ってリサイズ → レターボックス
        return ResizeAndLetterbox(src);
    }

    private static Mat PadToTarget(Mat src, int padTop, int padBottom)
    {
        var horizontal = TargetWidth - src.Width;
        var left = horizontal / 2;
        var right = horizontal - left;

        var dst = new Mat();
        Cv2.CopyMakeBorder(src, dst, padTop, padBottom, left, right, BorderTypes.Constant, Scalar.Black);
        return dst;
    }

    private static Mat ResizeAndLetterbox(Mat src)
    {
        var srcAspect = (double)src.Width / src.Height;
        var dstAspect = (double)TargetWidth / TargetHeight;

        int resizeW;
        int resizeH;
        if (srcAspect > dstAspect)
        {
            resizeW = TargetWidth;
            resizeH = (int)Math.Round(TargetWidth / srcAspect);
        }
        else
        {
            resizeH = TargetHeight;
            resizeW = (int)Math.Round(TargetHeight * srcAspect);
        }

        using var resized = new Mat();
        Cv2.Resize(src, resized, new Size(resizeW, resizeH), 0, 0, InterpolationFlags.Area);

        var padTop = (TargetHeight - resizeH) / 2;
        var padBottom = TargetHeight - resizeH - padTop;
        var padLeft = (TargetWidth - resizeW) / 2;
        var padRight = TargetWidth - resizeW - padLeft;

        var dst = new Mat();
        Cv2.CopyMakeBorder(resized, dst, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.Black);
        return dst;
    }
}
