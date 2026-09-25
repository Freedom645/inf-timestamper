using InfTimestamper.Core.YouTube;

namespace InfTimestamper.Core.Tests.YouTube;

public class YouTubeBroadcastMatcherTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 25, 20, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public void PicksClosestWithinTolerance()
    {
        var broadcasts = new[]
        {
            new YouTubeBroadcast("far", "遠い", StartedAt.AddMinutes(-8)),
            new YouTubeBroadcast("near", "近い", StartedAt.AddSeconds(15)),
        };

        var match = YouTubeBroadcastMatcher.FindClosest(broadcasts, StartedAt, TimeSpan.FromMinutes(10));

        Assert.Equal("near", match?.Id);
    }

    [Fact]
    public void OutsideTolerance_ReturnsNull()
    {
        var broadcasts = new[] { new YouTubeBroadcast("old", "昨日の配信", StartedAt.AddHours(-3)) };

        Assert.Null(YouTubeBroadcastMatcher.FindClosest(broadcasts, StartedAt, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void WithoutActualStartTime_IsIgnored()
    {
        var broadcasts = new[] { new YouTubeBroadcast("pending", "開始前", null) };

        Assert.Null(YouTubeBroadcastMatcher.FindClosest(broadcasts, StartedAt, TimeSpan.FromMinutes(10)));
    }
}
