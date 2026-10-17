using System.Text.Json;
using InfTimestamper.Core.DakenCounter;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.Tests.DakenCounter;

public class DakenCounterFieldMapperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void MapResult_RealEntry_ExpandsEveryInfinitasIdentifier()
    {
        // docs/sample/daken-counter/probe.jsonl の today_updates の 1 エントリ（Reflux の記録と突き合わせ済み）
        var item = Parse("""
            {"chart_id":"fb2f63bd","title":"Din Don Dan","difficulty":"SPH","lv":"7","notes":727,"score":1366,
             "bp":1,"dead":false,"lamp":6,"lamp_text":"EXH-CLEAR","score_rate":0.9394773039889959,
             "opt":"RANDOM","battle":false,"playspeed":1.0,"timestamp":1792193592}
            """);

        var fields = DakenCounterFieldMapper.MapResult(item);

        Assert.Equal("Din Don Dan", fields[FieldKeys.Title]);
        Assert.Equal("7", fields[FieldKeys.Level]);
        Assert.Equal("SPH", fields[FieldKeys.DiffShort]);
        Assert.Equal("HYPER", fields[FieldKeys.DiffLong]);
        Assert.Equal("1366", fields[FieldKeys.ExScore]);
        Assert.Equal("1", fields[FieldKeys.MissCount]);
        Assert.Equal("EX-HARD", fields[FieldKeys.Lamp]);
        Assert.Equal("AAA", fields[FieldKeys.DjLevel]);
    }

    [Theory]
    [InlineData(1, "FAILED")]
    [InlineData(2, "A-EASY")]
    [InlineData(3, "EASY")]
    [InlineData(4, "NORMAL")]
    [InlineData(5, "HARD")]
    [InlineData(6, "EX-HARD")]
    [InlineData(7, "FC")]
    public void MapResult_Lamp_UsesRefluxNotation(int lamp, string expected)
    {
        var fields = DakenCounterFieldMapper.MapResult(Parse($$"""{"lamp":{{lamp}}}"""));

        Assert.Equal(expected, fields[FieldKeys.Lamp]);
    }

    [Fact]
    public void MapResult_NoPlayLamp_IsOmitted()
    {
        var fields = DakenCounterFieldMapper.MapResult(Parse("""{"lamp":0}"""));

        Assert.False(fields.ContainsKey(FieldKeys.Lamp));
    }

    [Fact]
    public void MapResult_DeadPlay_KeepsBpAsIs()
    {
        // 途中落ちは落ちた後のノーツも POOR に数えた値になるが、Reflux と同じくそのまま出す
        var fields = DakenCounterFieldMapper.MapResult(Parse("""{"bp":273,"dead":true,"lamp":1}"""));

        Assert.Equal("273", fields[FieldKeys.MissCount]);
    }

    [Theory]
    // 理論値 1000（500 ノーツ）に対する 9 分割の境界。境界値ちょうどは上のランクに入る
    [InlineData(889, 500, "AAA")]
    [InlineData(888, 500, "AA")]
    [InlineData(778, 500, "AA")]
    [InlineData(777, 500, "A")]
    [InlineData(667, 500, "A")]
    [InlineData(666, 500, "B")]
    [InlineData(556, 500, "B")]
    [InlineData(445, 500, "C")]
    [InlineData(334, 500, "D")]
    [InlineData(223, 500, "E")]
    [InlineData(222, 500, "F")]
    [InlineData(0, 500, "F")]
    // 9 で割り切れる理論値（900）では境界ちょうどが上のランク
    [InlineData(800, 450, "AAA")]
    [InlineData(799, 450, "AA")]
    public void ResolveDjLevel_UsesNinthsOfMaxScore(int exScore, int notes, string expected)
    {
        Assert.Equal(expected, DakenCounterFieldMapper.ResolveDjLevel(exScore, notes));
    }

    [Fact]
    public void MapResult_WithoutNotes_OmitsDjLevel()
    {
        var fields = DakenCounterFieldMapper.MapResult(Parse("""{"score":1000,"notes":""}"""));

        Assert.Equal("1000", fields[FieldKeys.ExScore]);
        Assert.False(fields.ContainsKey(FieldKeys.DjLevel));
    }

    [Theory]
    [InlineData("SPA", "SPA", "ANOTHER")]
    [InlineData("DPH", "DPH", "HYPER")]
    [InlineData("SPL", "SPL", "LEGGENDARIA")]
    [InlineData("dbn", "DBN", "NORMAL")]
    public void MapCurrentSong_Difficulty(string raw, string diffShort, string diffLong)
    {
        var fields = DakenCounterFieldMapper.MapCurrentSong(Parse($$"""{"music":"Song","difficulty":"{{raw}}","lv":"11"}"""));

        Assert.Equal(diffShort, fields[FieldKeys.DiffShort]);
        Assert.Equal(diffLong, fields[FieldKeys.DiffLong]);
    }

    [Fact]
    public void MapCurrentSong_ReadsTitleFromMusic()
    {
        // history_cursong の曲名は "music"（today_updates の "title" とキー名が違う）
        var fields = DakenCounterFieldMapper.MapCurrentSong(Parse("""
            {"chart_id":"b67c","music":"Plan 8","difficulty":"SPA","lv":"12","notes":1690,"best_score":2534}
            """));

        Assert.Equal("Plan 8", fields[FieldKeys.Title]);
        Assert.Equal("12", fields[FieldKeys.Level]);
        Assert.Equal("SPA", fields[FieldKeys.DiffShort]);
        // 自己ベスト（best_*）は取り込まない
        Assert.False(fields.ContainsKey(FieldKeys.ExScore));
    }

    [Fact]
    public void MapCurrentSong_ChartNotRecognized_KeepsTitleOnly()
    {
        // 選曲画面で譜面まで読めていないとき（difficulty / lv が空）
        var fields = DakenCounterFieldMapper.MapCurrentSong(Parse("""
            {"chart_id":null,"lv":"","music":"Plan 8","difficulty":"","notes":""}
            """));

        Assert.Equal("Plan 8", fields[FieldKeys.Title]);
        Assert.Single(fields);
    }

    [Fact]
    public void TryGetResultTime_ReadsUnixSeconds()
    {
        Assert.True(DakenCounterFieldMapper.TryGetResultTime(Parse("""{"timestamp":1792193592}"""), out var time));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792193592), time);

        Assert.False(DakenCounterFieldMapper.TryGetResultTime(Parse("""{"timestamp":0}"""), out _));
        Assert.False(DakenCounterFieldMapper.TryGetResultTime(Parse("""{}"""), out _));
    }
}
