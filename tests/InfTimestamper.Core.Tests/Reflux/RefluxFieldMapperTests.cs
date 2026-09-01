using System.Text.Json;
using InfTimestamper.Core.Reflux;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Reflux;

public class RefluxFieldMapperTests
{
    [Fact]
    public void Map_FullResult_MapsAllIdentifiers()
    {
        var json = new RefluxLatestJson
        {
            Title = "GIGA RAID",
            Level = "11",
            Diff = "ANOTHER",
            PlayType = "SP",
            Grade = "AAA",
            Lamp = "FC",
            ExScore = "1820",
            Bad = "3",
            Poor = "2",
        };

        var fields = RefluxFieldMapper.Map(json);

        Assert.Equal("GIGA RAID", fields["title"]);
        Assert.Equal("11", fields["level"]);
        Assert.Equal("ANOTHER", fields["diff_l"]);
        Assert.Equal("SPA", fields["diff_s"]);
        Assert.Equal("AAA", fields["dj_level"]);
        Assert.Equal("FC", fields["lamp"]);
        Assert.Equal("1820", fields["ex_score"]);
        Assert.Equal("5", fields["miss_count"]); // bad + poor
    }

    [Theory]
    [InlineData("SP", "BEGINNER", "SPB", "BEGINNER")]
    [InlineData("DP", "NORMAL", "DPN", "NORMAL")]
    [InlineData("SP", "HYPER", "SPH", "HYPER")]
    [InlineData("DP", "ANOTHER", "DPA", "ANOTHER")]
    [InlineData("SP", "LEGGENDARIA", "SPL", "LEGGENDARIA")]
    [InlineData("DP", "A", "DPA", "ANOTHER")] // 略字入力も解釈
    public void Map_Difficulty_ResolvesShortAndLong(string playType, string diff, string expectedShort, string expectedLong)
    {
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { PlayType = playType, Diff = diff });

        Assert.Equal(expectedShort, fields["diff_s"]);
        Assert.Equal(expectedLong, fields["diff_l"]);
    }

    [Fact]
    public void Map_DiffWithSidePrefix_StripsAndUsesIt()
    {
        // playtype が無くても diff 接頭辞から SP/DP を拾う
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Diff = "SPA" });
        Assert.Equal("SPA", fields["diff_s"]);
        Assert.Equal("ANOTHER", fields["diff_l"]);
    }

    [Theory]
    [InlineData("F", "FAILED")]
    [InlineData("AC", "A-EASY")]
    [InlineData("EC", "EASY")]
    [InlineData("NC", "NORMAL")]
    [InlineData("HC", "HARD")]
    [InlineData("EX", "EX-HARD")]
    [InlineData("FC", "FC")]
    [InlineData("PFC", "FC")]
    public void Map_Lamp_MapsToRequirementSet(string refluxLamp, string expected)
    {
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Lamp = refluxLamp });
        Assert.Equal(expected, fields["lamp"]);
    }

    [Fact]
    public void Map_NoPlayLamp_OmitsLamp()
    {
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Lamp = "NP" });
        Assert.False(fields.ContainsKey("lamp"));
    }

    [Fact]
    public void Map_MissingValues_OmitsKeys()
    {
        // unknown / -1 / 空文字は欠損扱い
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson
        {
            Title = "unknown",
            Level = "-1",
            Diff = "",
            Grade = "",
            Lamp = "",
            ExScore = "-1",
            Bad = "-1",
            Poor = "0",
        });

        Assert.Empty(fields);
    }

    [Fact]
    public void Map_MissCount_RequiresBothBadAndPoor()
    {
        // poor 欠損なら miss_count は出さない
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Bad = "3", Poor = "-1" });
        Assert.False(fields.ContainsKey("miss_count"));
    }

    [Fact]
    public void Map_InvalidGrade_OmitsDjLevel()
    {
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Grade = "ZZ" });
        Assert.False(fields.ContainsKey("dj_level"));
    }

    [Fact]
    public void Map_PlayTypeIsPlayerSide_DoesNotLeakIntoDiffShort()
    {
        // 実出力の playtype は "P1"/"P2"（プレイヤーサイド）で SP/DP ではない。
        // SP/DP は diff の接頭辞から取る。
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson
        {
            Diff = "SPN",
            PlayType = "P2",
            Style = "MIRROR",
        });

        Assert.Equal("SPN", fields["diff_s"]);
        Assert.Equal("NORMAL", fields["diff_l"]);
    }

    [Fact]
    public void Map_DiffPrefixWinsOverPlayType()
    {
        var fields = RefluxFieldMapper.Map(new RefluxLatestJson { Diff = "DPA", PlayType = "SP" });
        Assert.Equal("DPA", fields["diff_s"]);
    }

    /// <summary>
    /// 実機 Reflux の出力サンプル（docs/sample/reflux/latest.json）を通し、
    /// 全識別子が期待どおり展開されることを確認する。
    /// </summary>
    [Fact]
    public void Map_RealSampleFile_ExpandsAllIdentifiers()
    {
        var path = TestPaths.RepositoryFile("docs", "sample", "reflux", "latest.json");
        Assert.True(File.Exists(path), $"サンプルが見つかりません: {path}");

        var json = JsonSerializer.Deserialize<RefluxLatestJson>(File.ReadAllText(path));
        Assert.NotNull(json);

        var fields = RefluxFieldMapper.Map(json!);

        Assert.Equal("ACTØ", fields["title"]);
        Assert.Equal("6", fields["level"]);
        Assert.Equal("NORMAL", fields["diff_l"]);
        Assert.Equal("SPN", fields["diff_s"]);
        Assert.Equal("D", fields["dj_level"]);
        Assert.Equal("FAILED", fields["lamp"]);
        Assert.Equal("431", fields["ex_score"]);
        Assert.Equal("9", fields["miss_count"]); // bad=2 + poor=7
    }
}
