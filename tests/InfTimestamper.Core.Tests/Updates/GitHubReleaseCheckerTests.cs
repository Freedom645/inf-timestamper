using System.Net;
using System.Net.Http;
using System.Text;
using InfTimestamper.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Updates;

public class GitHubReleaseCheckerTests
{
    private static GitHubReleaseChecker BuildChecker(StubHandler handler)
    {
        var client = new HttpClient(handler);
        return new GitHubReleaseChecker(client, "Freedom645/inf-timestamper", NullLogger<GitHubReleaseChecker>.Instance);
    }

    private static StubHandler JsonHandler(string json, Action<HttpRequestMessage>? observe = null)
        => new(req =>
        {
            observe?.Invoke(req);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

    [Fact]
    public async Task GetLatestReleaseAsync_OnSuccess_ReturnsParsedRelease()
    {
        var json = """
        {
          "tag_name": "v1.2.3",
          "name": "Release 1.2.3",
          "html_url": "https://github.com/Freedom645/inf-timestamper/releases/tag/v1.2.3",
          "published_at": "2026-05-01T12:00:00Z"
        }
        """;
        var checker = BuildChecker(JsonHandler(json));

        var release = await checker.GetLatestReleaseAsync(false, CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("v1.2.3", release!.TagName);
        Assert.Equal("Release 1.2.3", release.Name);
        Assert.Contains("v1.2.3", release.HtmlUrl);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), release.PublishedAt);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(false, CancellationToken.None);
        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RateLimited_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(false, CancellationToken.None);
        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NetworkError_ReturnsNull()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("接続できません"));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(false, CancellationToken.None);
        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_StableOnly_UsesTheLatestEndpoint()
    {
        // releases/latest はプレリリースを含まないので、正式版の利用者に α 版が見えることはない
        Uri? requested = null;
        var checker = BuildChecker(JsonHandler("""{"tag_name":"v1.1.0"}""", req => requested = req.RequestUri));

        await checker.GetLatestReleaseAsync(false, CancellationToken.None);

        Assert.EndsWith("/releases/latest", requested!.AbsolutePath);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_IncludingPrerelease_PicksTheHighestNonDraftVersion()
    {
        // 一覧は作成日時順で、バージョン順とは限らない。ドラフトと解釈できないタグは除外する
        var json = """
        [
          { "tag_name": "v1.2.0-alpha.1", "name": "alpha 1", "html_url": "https://example/a1", "published_at": "2026-09-20T00:00:00Z", "draft": false, "prerelease": true },
          { "tag_name": "v1.3.0", "name": "draft", "html_url": "https://example/draft", "published_at": null, "draft": true, "prerelease": false },
          { "tag_name": "v1.2.0-alpha.2", "name": "alpha 2", "html_url": "https://example/a2", "published_at": "2026-09-21T00:00:00Z", "draft": false, "prerelease": true },
          { "tag_name": "v1.1.0", "name": "stable", "html_url": "https://example/s", "published_at": "2026-09-01T00:00:00Z", "draft": false, "prerelease": false },
          { "tag_name": "not-a-version", "name": "junk", "html_url": "https://example/j", "published_at": "2026-09-22T00:00:00Z", "draft": false, "prerelease": false }
        ]
        """;
        Uri? requested = null;
        var checker = BuildChecker(JsonHandler(json, req => requested = req.RequestUri));

        var release = await checker.GetLatestReleaseAsync(true, CancellationToken.None);

        Assert.EndsWith("/releases", requested!.AbsolutePath);
        Assert.NotNull(release);
        Assert.Equal("v1.2.0-alpha.2", release!.TagName);
        Assert.True(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_IncludingPrerelease_PrefersAStableAboveThePrerelease()
    {
        var json = """
        [
          { "tag_name": "v1.2.0-alpha.2", "name": "alpha", "html_url": "https://example/a", "published_at": "2026-09-21T00:00:00Z", "draft": false, "prerelease": true },
          { "tag_name": "v1.2.0", "name": "stable", "html_url": "https://example/s", "published_at": "2026-09-25T00:00:00Z", "draft": false, "prerelease": false }
        ]
        """;
        var checker = BuildChecker(JsonHandler(json));

        var release = await checker.GetLatestReleaseAsync(true, CancellationToken.None);

        Assert.Equal("v1.2.0", release!.TagName);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_IncludingPrerelease_EmptyList_ReturnsNull()
    {
        var checker = BuildChecker(JsonHandler("[]"));

        Assert.Null(await checker.GetLatestReleaseAsync(true, CancellationToken.None));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_factory(request));
    }
}
