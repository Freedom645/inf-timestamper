using InfTimestamper.Core.Updates;

namespace InfTimestamper.Core.Tests.Updates;

internal sealed class FakeUpdateService : IUpdateService
{
    public bool IsInstalled { get; set; }
    public bool DownloadResult { get; set; }
    public int ApplyAndRestartCallCount { get; private set; }
    public int CheckAndDownloadCallCount { get; private set; }
    public Exception? CheckAndDownloadException { get; set; }

    public Task<bool> CheckAndDownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        CheckAndDownloadCallCount++;
        if (CheckAndDownloadException is not null) throw CheckAndDownloadException;
        // ダミーの進捗報告
        progress?.Report(50);
        progress?.Report(100);
        return Task.FromResult(DownloadResult);
    }

    public void ApplyAndRestart() => ApplyAndRestartCallCount++;
}
