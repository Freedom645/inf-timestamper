using InfTimestamper.Services;

namespace InfTimestamper.Core.Tests.ViewModels;

internal sealed class FakeClipboardService : IClipboardService
{
    public string LastText { get; private set; } = string.Empty;
    public int CallCount { get; private set; }

    public void SetText(string text)
    {
        LastText = text;
        CallCount++;
    }
}
