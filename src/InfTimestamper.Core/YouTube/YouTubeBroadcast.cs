namespace InfTimestamper.Core.YouTube;

/// <summary>
/// 配信中のライブ 1 件。<see cref="Id"/> はそのまま動画 ID として <c>videos.update</c> に渡せる。
/// </summary>
/// <param name="ActualStartTime">YouTube 側で配信が始まった時刻。配信開始直後は未確定のことがある。</param>
public sealed record YouTubeBroadcast(string Id, string Title, DateTimeOffset? ActualStartTime);

/// <summary>記録の配信開始時間に対応するライブを選ぶ。</summary>
public static class YouTubeBroadcastMatcher
{
    /// <summary>配信開始時間とライブの開始時刻の許容差の既定値。</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 開始時刻が <paramref name="streamStartedAt"/> から <paramref name="tolerance"/> 以内のライブのうち、
    /// 最も近いものを返す。該当が無ければ null。
    /// </summary>
    public static YouTubeBroadcast? FindClosest(
        IEnumerable<YouTubeBroadcast> broadcasts,
        DateTimeOffset streamStartedAt,
        TimeSpan tolerance)
    {
        return broadcasts
            .Where(b => b.ActualStartTime is not null)
            .Select(b => (Broadcast: b, Diff: (b.ActualStartTime!.Value - streamStartedAt).Duration()))
            .Where(x => x.Diff <= tolerance)
            .OrderBy(x => x.Diff)
            .Select(x => x.Broadcast)
            .FirstOrDefault();
    }
}
