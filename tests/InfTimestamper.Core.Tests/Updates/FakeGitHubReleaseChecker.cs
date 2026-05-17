using InfTimestamper.Core.Updates;

namespace InfTimestamper.Core.Tests.Updates;

internal sealed class FakeGitHubReleaseChecker : IGitHubReleaseChecker
{
    public GitHubRelease? NextResult { get; set; }
    public Exception? NextException { get; set; }
    public int CallCount { get; private set; }

    public Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        if (NextException is not null) throw NextException;
        return Task.FromResult(NextResult);
    }
}
