using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class SongTitleMatcherTests
{
    private static SongRepository BuildRepository() => new(new[]
    {
        new SongRecord("1", "GIGA RAID", "GIGARAID", new Dictionary<string, int>()),
        new SongRecord("2", "20,November", "20NOVEMBER", new Dictionary<string, int>()),
        new SongRecord("3", "five seconds until dawn", "FIVESECONDSUNTILDAWN", new Dictionary<string, int>()),
    });

    [Fact]
    public void Match_ExactMatch_ReturnsConfirmed()
    {
        var matcher = new SongTitleMatcher(BuildRepository());
        var result = matcher.Match("GIGA RAID");

        Assert.Equal(SongMatchKind.Confirmed, result.Kind);
        Assert.Equal("GIGA RAID", result.Title);
        Assert.Equal(0, result.Distance);
    }

    [Fact]
    public void Match_OneCharTypo_ReturnsCandidateWithinThreshold()
    {
        var matcher = new SongTitleMatcher(BuildRepository());
        var result = matcher.Match("GIGA RA1D"); // I → 1

        Assert.Equal(SongMatchKind.Candidate, result.Kind);
        Assert.NotNull(result.Record);
        Assert.Equal("GIGA RAID", result.Record!.Title);
        Assert.Equal(1, result.Distance);
    }

    [Fact]
    public void Match_BeyondThreshold_ReturnsUnmatched()
    {
        var matcher = new SongTitleMatcher(BuildRepository());
        // "ZZZZ" は GIGARAID と距離 8、20NOVEMBER と距離 10
        // 短い入力 (len 4) なので threshold = min(3, ceil(4*0.3)) = 2
        var result = matcher.Match("ZZZZ");

        Assert.Equal(SongMatchKind.Unmatched, result.Kind);
        Assert.Equal("ZZZZ", result.Title); // 生 OCR をそのまま返す
    }

    [Fact]
    public void Match_EmptyInput_ReturnsUnmatched()
    {
        var matcher = new SongTitleMatcher(BuildRepository());
        var result = matcher.Match("");

        Assert.Equal(SongMatchKind.Unmatched, result.Kind);
    }

    [Fact]
    public void Match_ThresholdScalesWithLength()
    {
        var matcher = new SongTitleMatcher(BuildRepository());
        // FIVESECONDSUNTILDAWN (len 20) → threshold = min(3, ceil(20*0.3)) = 3
        // 3 文字差なら候補一致（正規化後の文字位置で 3 文字置換）
        var result = matcher.Match("fixe secxnds until daxn"); // v→x, o→x, w→x の置換 3 つ

        Assert.Equal(SongMatchKind.Candidate, result.Kind);
        Assert.Equal("five seconds until dawn", result.Record!.Title);
        Assert.Equal(3, result.Distance);
    }
}
