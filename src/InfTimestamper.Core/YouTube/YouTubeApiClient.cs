using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InfTimestamper.Core.YouTube;

/// <summary>概要欄の同期に使う YouTube Data API v3 の呼び出し。</summary>
public interface IYouTubeApi
{
    /// <summary>ログイン済みで API を呼べる状態か。</summary>
    bool IsAvailable { get; }

    /// <summary>ログイン中のアカウントの配信中ライブ（<c>liveBroadcasts.list</c>、1 ユニット）。</summary>
    Task<IReadOnlyList<YouTubeBroadcast>> ListActiveBroadcastsAsync(CancellationToken cancellationToken);

    /// <summary>動画の snippet（<c>videos.list</c>、1 ユニット）。動画が無ければ null。</summary>
    Task<JsonObject?> GetVideoSnippetAsync(string videoId, CancellationToken cancellationToken);

    /// <summary>動画の snippet を書き換える（<c>videos.update</c>、50 ユニット）。</summary>
    Task UpdateVideoSnippetAsync(string videoId, JsonObject snippet, CancellationToken cancellationToken);

    /// <summary>ログイン中のアカウントのチャンネル名（<c>channels.list</c>、1 ユニット）。</summary>
    Task<string?> GetMyChannelTitleAsync(CancellationToken cancellationToken);
}

public sealed class YouTubeApiClient : IYouTubeApi
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3/";

    private readonly HttpClient _http;
    private readonly IYouTubeAccessTokenSource _tokens;

    public YouTubeApiClient(HttpClient http, IYouTubeAccessTokenSource tokens)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public bool IsAvailable => _tokens.IsSignedIn;

    public async Task<IReadOnlyList<YouTubeBroadcast>> ListActiveBroadcastsAsync(CancellationToken cancellationToken)
    {
        // broadcastStatus を指定すると mine は指定できない（認可ユーザのライブに限られる）
        using var doc = await SendAsync(HttpMethod.Get,
            "liveBroadcasts?part=snippet&broadcastStatus=active&broadcastType=all&maxResults=50",
            body: null, cancellationToken).ConfigureAwait(false);

        var result = new List<YouTubeBroadcast>();
        if (!doc.RootElement.TryGetProperty("items", out var items)) return result;

        foreach (var item in items.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            if (string.IsNullOrEmpty(id)) continue;

            var snippet = item.GetProperty("snippet");
            var title = snippet.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            DateTimeOffset? startedAt = snippet.TryGetProperty("actualStartTime", out var s)
                && s.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(s.GetString(), out var parsed)
                    ? parsed
                    : null;
            result.Add(new YouTubeBroadcast(id, title, startedAt));
        }
        return result;
    }

    public async Task<JsonObject?> GetVideoSnippetAsync(string videoId, CancellationToken cancellationToken)
    {
        using var doc = await SendAsync(HttpMethod.Get,
            "videos?part=snippet&id=" + Uri.EscapeDataString(videoId),
            body: null, cancellationToken).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;
        return JsonNode.Parse(items[0].GetProperty("snippet").GetRawText()) as JsonObject;
    }

    public async Task UpdateVideoSnippetAsync(string videoId, JsonObject snippet, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["id"] = videoId,
            ["snippet"] = snippet.DeepClone(),
        };
        using var _ = await SendAsync(HttpMethod.Put, "videos?part=snippet", body.ToJsonString(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetMyChannelTitleAsync(CancellationToken cancellationToken)
    {
        using var doc = await SendAsync(HttpMethod.Get, "channels?part=snippet&mine=true",
            body: null, cancellationToken).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;
        return items[0].GetProperty("snippet").TryGetProperty("title", out var title) ? title.GetString() : null;
    }

    /// <summary>
    /// API を呼ぶ。401 はアクセストークンの期限切れとみなし、1 回だけ取り直して再送する。
    /// </summary>
    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string relativeUrl,
        string? body,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await _tokens.GetAccessTokenAsync(forceRefresh: attempt > 0, cancellationToken)
                .ConfigureAwait(false);

            using var request = new HttpRequestMessage(method, BaseUrl + relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                continue;

            if (!response.IsSuccessStatusCode)
                throw YouTubeApiException.FromResponse(response.StatusCode, raw);

            return JsonDocument.Parse(string.IsNullOrEmpty(raw) ? "{}" : raw);
        }
    }
}

/// <summary>YouTube Data API のエラー応答。</summary>
public sealed class YouTubeApiException : Exception
{
    public YouTubeApiException(HttpStatusCode statusCode, string? reason, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Reason = reason;
    }

    public HttpStatusCode StatusCode { get; }

    /// <summary><c>error.errors[0].reason</c>（<c>quotaExceeded</c> 等）。</summary>
    public string? Reason { get; }

    /// <summary>1 日のクォータを使い切った。翌日（太平洋時間の 0 時）まで回復しない。</summary>
    public bool IsQuotaExceeded => Reason is "quotaExceeded" or "dailyLimitExceeded";

    internal static YouTubeApiException FromResponse(HttpStatusCode statusCode, string raw)
    {
        string? reason = null;
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var m)) message = m.GetString();
                if (error.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                    && errors[0].TryGetProperty("reason", out var r))
                {
                    reason = r.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // HTML のエラーページ等。ステータスコードだけで扱う
        }

        return new YouTubeApiException(
            statusCode,
            reason,
            $"YouTube API エラー（HTTP {(int)statusCode}{(reason is null ? string.Empty : ", " + reason)}）: {message}");
    }
}
