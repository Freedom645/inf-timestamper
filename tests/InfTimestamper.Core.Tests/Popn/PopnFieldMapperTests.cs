using System.Text.Json;
using InfTimestamper.Core.Popn;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Popn;

public class PopnFieldMapperTests
{
    private static PopnResultJson MakeFull() => new()
    {
        Time = "2026-08-30 19:44:17",
        IdentifiedBy = "record-diff",
        Score = 93578,
        RankName = "AA",
        MedalName = "銅星",
        Judge = new PopnJudgeJson { Cool = 382, Great = 87, Good = 4, Bad = 2 },
        Music = new PopnMusicJson
        {
            Genre = "エモ",
            Title = "Sorrows",
            Artist = "Asako Yoshihiro",
            Sheet = "NORMAL",
            Level = 29,
        },
    };

    [Fact]
    public void Map_FullResult_MapsAllIdentifiers()
    {
        var fields = PopnFieldMapper.Map(MakeFull());

        Assert.Equal("Sorrows", fields["title"]);
        Assert.Equal("29", fields["level"]);
        Assert.Equal("NORMAL", fields["diff_l"]);
        Assert.Equal("N", fields["diff_s"]);
        Assert.Equal("AA", fields["rank"]);
        Assert.Equal("銅星", fields["medal"]);
        Assert.Equal("93578", fields["score"]);
        Assert.Equal("2", fields["bad"]);
    }

    [Fact]
    public void Map_DoesNotEmitInfinitasOnlyIdentifiers()
    {
        var fields = PopnFieldMapper.Map(MakeFull());

        // pop'n は成績系の識別子を固有名で持つ（ハイブリッド方針）
        Assert.False(fields.ContainsKey("dj_level"));
        Assert.False(fields.ContainsKey("lamp"));
        Assert.False(fields.ContainsKey("ex_score"));
        Assert.False(fields.ContainsKey("miss_count"));
    }

    [Theory]
    [InlineData("EASY", "E", "EASY")]
    [InlineData("NORMAL", "N", "NORMAL")]
    [InlineData("HYPER", "H", "HYPER")]
    [InlineData("EX", "EX", "EX")]
    [InlineData("normal", "N", "NORMAL")]
    public void Map_Sheet_ResolvesShortAndLong(string sheet, string expectedShort, string expectedLong)
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson
        {
            Music = new PopnMusicJson { Sheet = sheet },
        });

        Assert.Equal(expectedShort, fields["diff_s"]);
        Assert.Equal(expectedLong, fields["diff_l"]);
    }

    [Fact]
    public void Map_UnknownSheet_OmitsDifficulty()
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson
        {
            Music = new PopnMusicJson { Sheet = "ULTIMATE" },
        });

        Assert.False(fields.ContainsKey("diff_s"));
        Assert.False(fields.ContainsKey("diff_l"));
    }

    [Fact]
    public void Map_UnidentifiedChart_KeepsPlayResultButOmitsSongInfo()
    {
        // identified_by=特定できず のとき music / record が null になる。
        // 成績側はこのプレイの実測値なので残す。
        var fields = PopnFieldMapper.Map(new PopnResultJson
        {
            IdentifiedBy = "特定できず",
            Score = 84884,
            RankName = "A",
            MedalName = "銅菱",
            Judge = new PopnJudgeJson { Bad = 6 },
            Music = null,
        });

        Assert.False(fields.ContainsKey("title"));
        Assert.False(fields.ContainsKey("level"));
        Assert.False(fields.ContainsKey("diff_l"));
        Assert.False(fields.ContainsKey("diff_s"));

        Assert.Equal("84884", fields["score"]);
        Assert.Equal("A", fields["rank"]);
        Assert.Equal("銅菱", fields["medal"]);
        Assert.Equal("6", fields["bad"]);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("AAA")]
    [InlineData("AA")]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    public void Map_ValidRanks_AreKept(string rank)
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson { RankName = rank });
        Assert.Equal(rank, fields["rank"]);
    }

    [Theory]
    [InlineData("SSS")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Map_InvalidRank_IsOmitted(string? rank)
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson { RankName = rank });
        Assert.False(fields.ContainsKey("rank"));
    }

    [Fact]
    public void Map_AllKnownMedalNames_AreKept()
    {
        foreach (var medal in PopnFieldMapper.KnownMedalNames)
        {
            var fields = PopnFieldMapper.Map(new PopnResultJson { MedalName = medal });
            Assert.Equal(medal, fields["medal"]);
        }
    }

    [Fact]
    public void Map_UnknownMedalName_IsKept()
    {
        // メダル名は過去に改名された実績があるため、集合で弾かず素通しする
        var fields = PopnFieldMapper.Map(new PopnResultJson { MedalName = "虹星" });
        Assert.Equal("虹星", fields["medal"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100001)]
    public void Map_OutOfRangeScore_IsOmitted(int score)
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson { Score = score });
        Assert.False(fields.ContainsKey("score"));
    }

    [Fact]
    public void Map_ZeroValues_AreKept()
    {
        var fields = PopnFieldMapper.Map(new PopnResultJson
        {
            Score = 0,
            Judge = new PopnJudgeJson { Bad = 0 },
        });

        Assert.Equal("0", fields["score"]);
        Assert.Equal("0", fields["bad"]);
    }

    [Fact]
    public void Map_MissingLevel_IsOmitted()
    {
        // レベルは 1 以下なら譜面なし扱い（music.tsv の lv_* と同じ規約）
        var fields = PopnFieldMapper.Map(new PopnResultJson
        {
            Music = new PopnMusicJson { Title = "X", Level = 0 },
        });

        Assert.Equal("X", fields["title"]);
        Assert.False(fields.ContainsKey("level"));
    }

    [Fact]
    public void Map_EmptyResult_ReturnsEmpty()
    {
        Assert.Empty(PopnFieldMapper.Map(new PopnResultJson()));
    }

    [Fact]
    public void TryParseTime_ParsesTrackerFormatAsLocalTime()
    {
        Assert.True(PopnFieldMapper.TryParseTime("2026-08-30 19:44:17", out var value));
        Assert.Equal(new DateTime(2026, 8, 30, 19, 44, 17), value.LocalDateTime);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(value.LocalDateTime), value.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026/08/30 19:44:17")]
    [InlineData("not a time")]
    public void TryParseTime_InvalidInput_ReturnsFalse(string? raw)
    {
        Assert.False(PopnFieldMapper.TryParseTime(raw, out _));
    }

    /// <summary>
    /// 実機 popn-lively-tracker の出力サンプル（docs/sample/popn-tracker/result.json）を
    /// そのまま通し、全識別子が期待どおり展開されることを確認する。
    /// </summary>
    [Fact]
    public void Map_RealSampleFile_ExpandsAllIdentifiers()
    {
        var path = TestPaths.RepositoryFile("docs", "sample", "popn-tracker", "result.json");
        Assert.True(File.Exists(path), $"サンプルが見つかりません: {path}");

        var json = JsonSerializer.Deserialize<PopnResultJson>(File.ReadAllText(path));
        Assert.NotNull(json);

        var fields = PopnFieldMapper.Map(json!);

        Assert.Equal("Sorrows", fields["title"]);
        Assert.Equal("29", fields["level"]);
        Assert.Equal("NORMAL", fields["diff_l"]);
        Assert.Equal("N", fields["diff_s"]);
        Assert.Equal("AA", fields["rank"]);
        Assert.Equal("銅星", fields["medal"]);
        Assert.Equal("93578", fields["score"]);
        Assert.Equal("2", fields["bad"]);

        // record セクション（自己ベスト）を取り違えていないこと。
        // サンプルでは record.score=92926 / previous_best=92926 で、このプレイのスコアとは別値。
        Assert.DoesNotContain("92926", fields.Values);
    }
}
