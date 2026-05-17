using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public interface IOcrService
{
    bool IsAvailable { get; }
    OcrResult? RecognizeDigits(Mat roi);
    OcrResult? RecognizeText(Mat roi);
}

public sealed record OcrResult(string Text, float MeanConfidence);
