using OpenCvSharp;

namespace InfTimestamper.Core.Recognition;

public interface IImageHasher
{
    ulong ComputeAverageHash(Mat roi);
    ulong ComputePerceptualHash(Mat roi);
}
