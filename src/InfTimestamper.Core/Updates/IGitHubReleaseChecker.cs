namespace InfTimestamper.Core.Updates;

public interface IGitHubReleaseChecker
{
    Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken);
}

public sealed record GitHubRelease(string TagName, string Name, string HtmlUrl, DateTimeOffset PublishedAt);
