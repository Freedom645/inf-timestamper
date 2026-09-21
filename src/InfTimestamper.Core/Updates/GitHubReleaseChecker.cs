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

    /// <summary>プレリリース込みで探すときに見るリリース数。α 版の間隔を考えればこれで足りる。</summary>
    private const int ListPageSize = 30;

    private readonly HttpClient _http;
    private readonly string _latestUrl;
    private readonly string _listUrl;
    private readonly ILogger<GitHubReleaseChecker> _logger;

    public GitHubReleaseChecker(HttpClient http)
        : this(http, DefaultRepository, NullLogger<GitHubReleaseChecker>.Instance) { }

    public GitHubReleaseChecker(HttpClient http, string repository, ILogger<GitHubReleaseChecker> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? NullLogger<GitHubReleaseChecker>.Instance;
        _latestUrl = $"https://api.github.com/repos/{repository}/releases/latest";
        _listUrl = $"https://api.github.com/repos/{repository}/releases?per_page={ListPageSize}";

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease, CancellationToken cancellationToken)
    {
        try
        {
            // releases/latest はプレリリースとドラフトを含まないので、正式版だけ見たいときはこれで十分
            if (!includePrerelease)
            {
                var dto = await GetAsync<ReleaseDto>(_latestUrl, cancellationToken).ConfigureAwait(false);
                return dto is null ? null : ToRelease(dto);
            }

            var list = await GetAsync<List<ReleaseDto>>(_listUrl, cancellationToken).ConfigureAwait(false);
            if (list is null) return null;

            // 一覧は作成日時順で返るが、バージョン順とは限らない（古いパッチを後から出す等）ので SemVer で選ぶ
            ReleaseDto? best = null;
            SemanticVersion? bestVersion = null;
            foreach (var dto in list)
            {
                if (dto.Draft) continue;
                if (!SemanticVersion.TryParse(dto.TagName, out var version)) continue;
                if (bestVersion is null || version.CompareTo(bestVersion) > 0)
                {
                    best = dto;
                    bestVersion = version;
                }
            }

            if (best is null)
            {
                _logger.LogInformation("GitHub にリリースが見つかりませんでした。");
                return null;
            }
            return ToRelease(best);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "GitHub Releases の取得に失敗しました。");
            return null;
        }
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
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
        return await response.Content
            .ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static GitHubRelease ToRelease(ReleaseDto dto) => new(
        dto.TagName ?? string.Empty,
        dto.Name ?? dto.TagName ?? string.Empty,
        dto.HtmlUrl ?? string.Empty,
        dto.PublishedAt ?? default,
        dto.Prerelease);

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        // ドラフトは published_at が null で返る
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}
