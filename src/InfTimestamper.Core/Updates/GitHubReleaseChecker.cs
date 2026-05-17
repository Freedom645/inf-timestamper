using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Updates;

public sealed class GitHubReleaseChecker : IGitHubReleaseChecker
{
    public const string DefaultRepository = "Freedom645/inf-timestamper";
    public const string UserAgent = "inf-timestamper";

    private readonly HttpClient _http;
    private readonly string _releaseUrl;
    private readonly ILogger<GitHubReleaseChecker> _logger;

    public GitHubReleaseChecker(HttpClient http)
        : this(http, DefaultRepository, NullLogger<GitHubReleaseChecker>.Instance) { }

    public GitHubReleaseChecker(HttpClient http, string repository, ILogger<GitHubReleaseChecker> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? NullLogger<GitHubReleaseChecker>.Instance;
        _releaseUrl = $"https://api.github.com/repos/{repository}/releases/latest";

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _releaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("GitHub にリリースが見つかりませんでした。");
                return null;
            }

            if ((int)response.StatusCode == 403)
            {
                _logger.LogWarning("GitHub API のレート制限を超過した可能性があります（HTTP 403）。");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var dto = await response.Content
                .ReadFromJsonAsync<ReleaseDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (dto is null) return null;

            return new GitHubRelease(
                dto.TagName ?? string.Empty,
                dto.Name ?? dto.TagName ?? string.Empty,
                dto.HtmlUrl ?? string.Empty,
                dto.PublishedAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "GitHub Releases の取得に失敗しました。");
            return null;
        }
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset PublishedAt { get; set; }
    }
}
