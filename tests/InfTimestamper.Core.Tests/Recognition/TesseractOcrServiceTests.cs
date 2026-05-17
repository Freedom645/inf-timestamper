using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class TesseractOcrServiceTests
{
    [Fact]
    public void Constructor_MissingTessdata_IsUnavailable()
    {
        using var service = new TesseractOcrService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.False(service.IsAvailable);
        Assert.Null(service.RecognizeDigits(new OpenCvSharp.Mat()));
        Assert.Null(service.RecognizeText(new OpenCvSharp.Mat()));
    }
}
