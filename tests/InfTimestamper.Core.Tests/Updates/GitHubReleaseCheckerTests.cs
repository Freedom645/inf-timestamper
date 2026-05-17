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

        var handler = new StubHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("v1.2.3", release!.TagName);
        Assert.Equal("Release 1.2.3", release.Name);
        Assert.Contains("v1.2.3", release.HtmlUrl);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), release.PublishedAt);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(CancellationToken.None);
        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RateLimited_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(CancellationToken.None);
        Assert.Null(release);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NetworkError_ReturnsNull()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("接続できません"));
        var checker = BuildChecker(handler);

        var release = await checker.GetLatestReleaseAsync(CancellationToken.None);
        Assert.Null(release);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_factory(request));
    }
}
