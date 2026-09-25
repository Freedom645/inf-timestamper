using System.Net.Http;
using System.Diagnostics;
using System.Text.Json.Nodes;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.Core.YouTube;
using Microsoft.Extensions.Time.Testing;

namespace InfTimestamper.Core.Tests.YouTube;

/// <summary>
/// 同期ループは実スレッドで回し、時間だけ <see cref="FakeTimeProvider"/> で進める。
/// ループが待機に入る前に時間を進めると待ちが伸びてしまうので、進める前に少しだけ実時間で待つ。
/// </summary>
public class YouTubeDescriptionSyncTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 25, 11, 0, 0, TimeSpan.Zero);

    private static readonly YouTubeSyncOptions Options = new(
        "▼タイムスタンプ", TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(10));

    private static (YouTubeDescriptionSync Sync, FakeYouTubeApi Api, FakeTimeProvider Time) Create(
        DateTimeOffset? actualStartTime = null,
        string description = "説明文")
    {
        var time = new FakeTimeProvider(StartedAt);
        var api = new FakeYouTubeApi(description, actualStartTime ?? StartedAt.AddSeconds(12));
        return (new YouTubeDescriptionSync(api, time: time), api, time);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("条件が満たされませんでした。");
            await Task.Delay(10);
        }
    }

    /// <summary>ループが次の待機に入るのを待ってから時間を進める。</summary>
    private static async Task AdvanceAsync(FakeTimeProvider time, TimeSpan by)
    {
        await Task.Delay(150);
        time.Advance(by);
    }

    [Fact]
    public async Task FindsBroadcastAndWritesDescription()
    {
        var (sync, api, _) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:00:00 配信開始", "00:05:00 曲A" });

        // Fake への書き込みと状態の更新は別タイミングなので、状態が「更新済み」になるまで待つ
        await WaitUntilAsync(() => sync.Status.LastUpdatedAt is not null);
        Assert.Equal("説明文\n\n▼タイムスタンプ\n00:00:00 配信開始\n00:05:00 曲A", api.Description);
        Assert.Equal(YouTubeSyncState.Linked, sync.Status.State);
        Assert.Equal("テスト配信", sync.Status.BroadcastTitle);
        Assert.NotNull(sync.Status.LastUpdatedAt);
    }

    [Fact]
    public async Task Update_PreservesWritableSnippetFields()
    {
        // videos.update は snippet を丸ごと置き換えるので、タグ等を送り返さないと消える
        var (sync, api, _) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => api.Updates.Count == 1);

        var sent = api.LastUpdatedSnippet;
        Assert.Equal("テスト配信", sent["title"]!.GetValue<string>());
        Assert.Equal("20", sent["categoryId"]!.GetValue<string>());
        Assert.Equal(2, sent["tags"]!.AsArray().Count);
        Assert.Null(sent["channelId"]);
    }

    [Fact]
    public async Task UpdatesWithinInterval_AreThrottled()
    {
        var (sync, api, time) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => api.Updates.Count == 1);

        sync.RequestUpdate(new[] { "00:05:00 曲A", "00:09:00 曲B" });
        sync.RequestUpdate(new[] { "00:05:00 曲A", "00:09:00 曲B", "00:13:00 曲C" });
        await Task.Delay(200);
        Assert.Single(api.Updates);

        await AdvanceAsync(time, TimeSpan.FromSeconds(60));
        await WaitUntilAsync(() => api.Updates.Count == 2);
        // 間引いた間の依頼は最新のものだけが書かれる
        Assert.EndsWith("00:13:00 曲C", api.Description);
    }

    [Fact]
    public async Task SameLines_AreNotRewritten()
    {
        var (sync, api, time) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => api.Updates.Count == 1);

        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await AdvanceAsync(time, TimeSpan.FromSeconds(120));
        await Task.Delay(200);

        Assert.Single(api.Updates);
    }

    [Fact]
    public async Task EndSession_WritesFinalWithoutWaitingInterval()
    {
        var (sync, api, _) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => api.Updates.Count == 1);

        await sync.EndSessionAsync(new[] { "00:05:00 曲A", "00:09:00 曲B" });

        Assert.Equal(2, api.Updates.Count);
        Assert.EndsWith("00:09:00 曲B", api.Description);
        Assert.False(sync.IsActive);
    }

    [Fact]
    public async Task BroadcastNotYetStarted_IsSearchedAgain()
    {
        // OBS の配信開始から YouTube 側で配信中になるまで少し遅れる
        var (sync, api, time) = Create();
        await using var _ = sync;
        api.Broadcasts.Clear();

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => api.ListCalls == 1);
        Assert.Equal(YouTubeSyncState.Searching, sync.Status.State);

        api.Broadcasts.Add(new YouTubeBroadcast(FakeYouTubeApi.VideoId, "テスト配信", StartedAt.AddSeconds(40)));
        await AdvanceAsync(time, YouTubeDescriptionSync.SearchRetryInterval);

        await WaitUntilAsync(() => sync.Status.LastUpdatedAt is not null);
        Assert.Single(api.Updates);
    }

    [Fact]
    public async Task NoBroadcastWithinTolerance_GivesUp()
    {
        var (sync, api, time) = Create(actualStartTime: StartedAt.AddHours(-2));
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        await WaitUntilAsync(() => api.ListCalls == 1);
        await AdvanceAsync(time, TimeSpan.FromMinutes(11));

        await WaitUntilAsync(() => sync.Status.State == YouTubeSyncState.NotFound);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task QuotaExceeded_StopsSync()
    {
        var (sync, api, _) = Create();
        await using var _ = sync;
        api.NextError = FakeYouTubeApi.QuotaExceeded();

        sync.BeginSession(StartedAt, Options);
        sync.RequestUpdate(new[] { "00:05:00 曲A" });

        await WaitUntilAsync(() => sync.Status.State == YouTubeSyncState.QuotaExceeded);
        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task TransientError_IsRetriedAfterInterval()
    {
        var (sync, api, time) = Create();
        await using var _ = sync;

        sync.BeginSession(StartedAt, Options);
        await WaitUntilAsync(() => sync.Status.State == YouTubeSyncState.Linked);

        api.NextError = new HttpRequestException("network");
        sync.RequestUpdate(new[] { "00:05:00 曲A" });
        await WaitUntilAsync(() => sync.Status.State == YouTubeSyncState.Error);
        Assert.Empty(api.Updates);

        await AdvanceAsync(time, TimeSpan.FromSeconds(60));
        await WaitUntilAsync(() => sync.Status is { State: YouTubeSyncState.Linked, LastUpdatedAt: not null });
        Assert.Single(api.Updates);
    }

    [Fact]
    public void NotSignedIn_DoesNotStartSession()
    {
        var (sync, api, _) = Create();
        api.IsAvailable = false;

        sync.BeginSession(StartedAt, Options);

        Assert.False(sync.IsActive);
        Assert.Equal(YouTubeSyncState.Inactive, sync.Status.State);
    }

    [Fact]
    public void BuildUpdateSnippet_KeepsOnlyWritableFields()
    {
        var current = new JsonObject
        {
            ["title"] = "t",
            ["description"] = "old",
            ["categoryId"] = "20",
            ["publishedAt"] = "2026-09-25T00:00:00Z",
            ["liveBroadcastContent"] = "live",
        };

        var snippet = YouTubeDescriptionSync.BuildUpdateSnippet(current, "new");

        Assert.Equal("new", snippet["description"]!.GetValue<string>());
        Assert.Equal("t", snippet["title"]!.GetValue<string>());
        Assert.Null(snippet["publishedAt"]);
        Assert.Null(snippet["liveBroadcastContent"]);
    }
}
