using System.Net;
using System.Text.Json.Nodes;
using InfTimestamper.Core.YouTube;

namespace InfTimestamper.Core.Tests.TestHelpers;

/// <summary>1 本の動画だけを持つ YouTube API の代役。書き込まれた概要欄を記録する。</summary>
public sealed class FakeYouTubeApi : IYouTubeApi
{
    private readonly object _gate = new();
    private readonly List<string> _updates = new();
    private JsonObject _snippet;

    public FakeYouTubeApi(string description = "", DateTimeOffset? actualStartTime = null)
    {
        _snippet = new JsonObject
        {
            ["title"] = "テスト配信",
            ["description"] = description,
            ["categoryId"] = "20",
            ["tags"] = new JsonArray("beatmania", "IIDX"),
            ["channelId"] = "UC_dummy",
        };
        Broadcasts.Add(new YouTubeBroadcast(VideoId, "テスト配信", actualStartTime));
    }

    public const string VideoId = "video123";

    public bool IsAvailable { get; set; } = true;

    public List<YouTubeBroadcast> Broadcasts { get; } = new();

    /// <summary>次の API 呼び出しで投げる例外（1 回だけ）。</summary>
    public Exception? NextError { get; set; }

    public int ListCalls { get; private set; }

    public string Description
    {
        get { lock (_gate) return _snippet["description"]!.GetValue<string>(); }
    }

    public JsonObject LastUpdatedSnippet { get; private set; } = new();

    public IReadOnlyList<string> Updates
    {
        get { lock (_gate) return _updates.ToList(); }
    }

    public Task<IReadOnlyList<YouTubeBroadcast>> ListActiveBroadcastsAsync(CancellationToken cancellationToken)
    {
        ThrowIfError();
        lock (_gate)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlyList<YouTubeBroadcast>>(Broadcasts.ToList());
        }
    }

    public Task<JsonObject?> GetVideoSnippetAsync(string videoId, CancellationToken cancellationToken)
    {
        ThrowIfError();
        lock (_gate)
        {
            return Task.FromResult(videoId == VideoId ? (JsonObject?)_snippet.DeepClone().AsObject() : null);
        }
    }

    public Task UpdateVideoSnippetAsync(string videoId, JsonObject snippet, CancellationToken cancellationToken)
    {
        ThrowIfError();
        lock (_gate)
        {
            LastUpdatedSnippet = snippet.DeepClone().AsObject();
            var next = _snippet.DeepClone().AsObject();
            next["description"] = snippet["description"]!.GetValue<string>();
            _snippet = next;
            _updates.Add(next["description"]!.GetValue<string>());
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetMyChannelTitleAsync(CancellationToken cancellationToken)
        => Task.FromResult<string?>("テストチャンネル");

    public static YouTubeApiException QuotaExceeded()
        => new(HttpStatusCode.Forbidden, "quotaExceeded", "quota");

    private void ThrowIfError()
    {
        Exception? error;
        lock (_gate)
        {
            error = NextError;
            NextError = null;
        }
        if (error is not null) throw error;
    }
}
