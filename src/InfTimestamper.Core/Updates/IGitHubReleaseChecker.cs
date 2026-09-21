namespace InfTimestamper.Core.Updates;

public interface IGitHubReleaseChecker
{
    /// <summary>
    /// 最新のリリースを取得する。
    /// <paramref name="includePrerelease"/> が false なら正式版のみ（GitHub の <c>releases/latest</c>）、
    /// true ならプレリリースも含めた中で最もバージョンの高いものを返す（α 版利用者が次の α 版を拾うため）。
    /// ドラフトはどちらでも対象外。
    /// </summary>
    Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease, CancellationToken cancellationToken);
}

public sealed record GitHubRelease(
    string TagName,
    string Name,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    bool IsPrerelease = false);
