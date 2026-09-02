using System.Text.Json;
using InfTimestamper.Core.Sdvx;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Sdvx;

public class SdvxFieldMapperTests
{
    [Fact]
    public void IsSongDecided_TrueOnlyForSongDecidedScreen()
    {
        using var decided = SdvxMessages.DataOf(SdvxMessages.NowPlayingFromSongDecided());
        using var select = SdvxMessages.DataOf(SdvxMessages.NowPlayingFromSelect());

        Assert.True(SdvxFieldMapper.IsSongDecided(decided.RootElement));
        Assert.False(SdvxFieldMapper.IsSongDecided(select.RootElement));
    }

    [Fact]
    public void IsSongDecided_FalseWhenImagesAreMissing()
    {
        using var document = JsonDocument.Parse("""{"title":"x","difficulty":"EXH"}""");
        Assert.False(SdvxFieldMapper.IsSongDecided(document.RootElement));
    }

    [Fact]
    public void MapNowPlaying_MapsSongInfoOnly()
    {
        using var data = SdvxMessages.DataOf(
            SdvxMessages.NowPlayingFromSongDecided("999", "MXM", 20));

        var fields = SdvxFieldMapper.MapNowPlaying(data.RootElement);

        Assert.Equal("999", fields["title"]);
        Assert.Equal("MXM", fields["diff_s"]);
        Assert.Equal("MAXIMUM", fields["diff_l"]);
        Assert.Equal("20", fields["level"]);

        // 曲決定の時点では成績は出ていない
        Assert.False(fields.ContainsKey("score"));
        Assert.False(fields.ContainsKey("clear_lamp"));
        Assert.False(fields.ContainsKey("grade"));
    }

    [Fact]
    public void MapNowPlaying_AcceptsLevelAsString()
    {
        using var data = SdvxMessages.DataOf(
            SdvxMessages.NowPlayingFromSongDecided(level: "18"));

        var fields = SdvxFieldMapper.MapNowPlaying(data.RootElement);

        Assert.Equal("18", fields["level"]);
    }

    [Fact]
    public void MapNowPlaying_SkipsUnknownLevel()
    {
        // SDVX Helper は楽曲 DB に無い曲でレベルを空文字列で送る
        using var data = SdvxMessages.DataOf(
            SdvxMessages.NowPlayingFromSongDecided(level: ""));

        var fields = SdvxFieldMapper.MapNowPlaying(data.RootElement);

        Assert.False(fields.ContainsKey("level"));
        Assert.Equal("Sample Song", fields["title"]);
    }

    [Fact]
    public void MapResult_MapsAllIdentifiers()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(
            DateTimeOffset.Now,
            title: "Garakuta Soul Pray",
            difficulty: "ADV",
            lv: "13",
            score: 9_915_670,
            exscore: 3210,
            grade: "AAA+",
            lamp: 5));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.Equal("Garakuta Soul Pray", fields["title"]);
        Assert.Equal("ADV", fields["diff_s"]);
        Assert.Equal("ADVANCED", fields["diff_l"]);
        Assert.Equal("13", fields["level"]);
        Assert.Equal("9915670", fields["score"]);
        Assert.Equal("9915", fields["score_short"]);
        Assert.Equal("3210", fields["ex_score"]);
        Assert.Equal("AAA+", fields["grade"]);
        Assert.Equal("UC", fields["clear_lamp"]);
    }

    [Fact]
    public void MapResult_DoesNotEmitOtherGameIdentifiers()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.False(fields.ContainsKey("dj_level"));
        Assert.False(fields.ContainsKey("lamp"));
        Assert.False(fields.ContainsKey("miss_count"));
        Assert.False(fields.ContainsKey("rank"));
        Assert.False(fields.ContainsKey("medal"));
        Assert.False(fields.ContainsKey("bad"));
    }

    [Theory]
    [InlineData("NOV", "NOV", "NOVICE")]
    [InlineData("ADV", "ADV", "ADVANCED")]
    [InlineData("EXH", "EXH", "EXHAUST")]
    [InlineData("MXM", "MXM", "MAXIMUM")]
    [InlineData("exh", "EXH", "EXHAUST")]
    [InlineData("INF", "INF", "INFINITE")]
    public void MapResult_Difficulty_ResolvesShortAndLong(string raw, string expectedShort, string expectedLong)
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, difficulty: raw));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.Equal(expectedShort, fields["diff_s"]);
        Assert.Equal(expectedLong, fields["diff_l"]);
    }

    [Fact]
    public void MapResult_UnknownDifficulty_FallsBackToTheRawName()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, difficulty: "NEW"));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.Equal("NEW", fields["diff_s"]);
        Assert.Equal("NEW", fields["diff_l"]);
    }

    [Theory]
    [InlineData(1, "PLAYED")]
    [InlineData(2, "COMP")]
    [InlineData(3, "EXC-COMP")]
    [InlineData(4, "MAXXIVE")]
    [InlineData(5, "UC")]
    [InlineData(6, "PUC")]
    public void MapResult_Lamp_MapsEnumValueToName(int lamp, string expected)
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, lamp: lamp));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.Equal(expected, fields["clear_lamp"]);
    }

    [Fact]
    public void MapResult_UnknownLamp_IsOmitted()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, lamp: 99));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.False(fields.ContainsKey("clear_lamp"));
    }

    [Fact]
    public void MapResult_ZeroExScore_IsOmitted()
    {
        // EX スコアはリザルト画面が EX スコア表示のときだけ読める
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, exscore: 0));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.False(fields.ContainsKey("ex_score"));
        Assert.True(fields.ContainsKey("score"));
    }

    [Fact]
    public void MapResult_OutOfRangeScore_IsOmitted()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(
            DateTimeOffset.Now, score: SdvxFieldMapper.MaxScore + 1));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.False(fields.ContainsKey("score"));
        Assert.False(fields.ContainsKey("score_short"));
    }

    [Fact]
    public void MapResult_UnknownGrade_IsOmitted()
    {
        using var item = ItemOf(SdvxMessages.ResultItem(DateTimeOffset.Now, grade: "SSS"));

        var fields = SdvxFieldMapper.MapResult(item.RootElement);

        Assert.False(fields.ContainsKey("grade"));
    }

    [Fact]
    public void TryGetResultTime_ReadsUnixSeconds()
    {
        var expected = new DateTimeOffset(2026, 9, 2, 20, 15, 30, TimeSpan.FromHours(9));
        using var item = ItemOf(SdvxMessages.ResultItem(expected));

        Assert.True(SdvxFieldMapper.TryGetResultTime(item.RootElement, out var actual));
        Assert.Equal(expected.ToUnixTimeSeconds(), actual.ToUnixTimeSeconds());
    }

    [Fact]
    public void TryGetResultTime_ReturnsFalseWhenMissing()
    {
        using var document = JsonDocument.Parse("""{"title":"x"}""");
        Assert.False(SdvxFieldMapper.TryGetResultTime(document.RootElement, out _));
    }

    private static JsonDocument ItemOf(object item)
        => JsonDocument.Parse(JsonSerializer.Serialize(item));
}
