using Microsoft.VisualBasic.FileIO;

namespace InfTimestamper.Services;

public sealed class WindowsFileRecycler : IFileRecycler
{
    public void SendToRecycleBin(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }
}
