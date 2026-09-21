using InfTimestamper.Core.Updates;

namespace InfTimestamper.Core.Tests.Updates;

internal sealed class FakeGitHubReleaseChecker : IGitHubReleaseChecker
{
    public GitHubRelease? NextResult { get; set; }
    public Exception? NextException { get; set; }
    public int CallCount { get; private set; }
    public bool? LastIncludePrerelease { get; private set; }

    public Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease, CancellationToken cancellationToken)
    {
        CallCount++;
        LastIncludePrerelease = includePrerelease;
        if (NextException is not null) throw NextException;
        return Task.FromResult(NextResult);
    }
}
