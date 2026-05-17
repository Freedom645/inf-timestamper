namespace InfTimestamper.Core.Recognition;

public sealed record Roi(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0 && X >= 0 && Y >= 0;
}
