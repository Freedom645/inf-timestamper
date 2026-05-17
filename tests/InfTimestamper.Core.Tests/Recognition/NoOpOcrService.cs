using InfTimestamper.Core.Recognition;
using OpenCvSharp;

namespace InfTimestamper.Core.Tests.Recognition;

internal sealed class NoOpOcrService : IOcrService
{
    public bool IsAvailable => false;
    public OcrResult? RecognizeDigits(Mat roi) => null;
    public OcrResult? RecognizeText(Mat roi) => null;
}
