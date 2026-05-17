namespace InfTimestamper.Services;

public interface IClipboardService
{
    void SetText(string text);
}

public sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            System.Windows.Clipboard.Clear();
            return;
        }
        System.Windows.Clipboard.SetText(text);
    }
}
