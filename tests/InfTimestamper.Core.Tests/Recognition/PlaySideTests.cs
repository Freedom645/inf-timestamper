using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class PlaySideTests
{
    [Theory]
    [InlineData("song_select/1p_arrow_center", PlaySide.OneP)]
    [InlineData("song_select/2p_arrow_center", PlaySide.TwoP)]
    [InlineData("song_select/1p_arrow_edge", PlaySide.OneP)]
    [InlineData("song_select/2p_arrow_edge", PlaySide.TwoP)]
    [InlineData("result/1p_pacemaker", PlaySide.OneP)]
    [InlineData("result/2p_pacemaker", PlaySide.TwoP)]
    [InlineData("1p_standalone", PlaySide.OneP)]
    [InlineData("2P_UPPERCASE", PlaySide.TwoP)]
    [InlineData("song_select/no_side_marker", PlaySide.Unknown)]
    [InlineData("", PlaySide.Unknown)]
    [InlineData(null, PlaySide.Unknown)]
    public void FromStateName_ParsesSideFromHashName(string? name, PlaySide expected)
    {
        Assert.Equal(expected, PlaySides.FromStateName(name));
    }

    [Fact]
    public void Suffix_ReturnsExpectedToken()
    {
        Assert.Equal("1p", PlaySides.Suffix(PlaySide.OneP));
        Assert.Equal("2p", PlaySides.Suffix(PlaySide.TwoP));
        Assert.Equal(string.Empty, PlaySides.Suffix(PlaySide.Unknown));
    }

    [Theory]
    [InlineData(PlaySide.OneP, "lamp_color_1p")]
    [InlineData(PlaySide.TwoP, "lamp_color_2p")]
    [InlineData(PlaySide.Unknown, "lamp_color_2p")] // Unknown は 2P 既定 (INFINITAS 配信のほとんどは 2P)
    public void RecognitionRoiKeys_LampColor_SelectsBySide(PlaySide side, string expectedKey)
    {
        Assert.Equal(expectedKey, RecognitionRoiKeys.LampColor(side));
    }

    [Theory]
    [InlineData("miss_count", PlaySide.OneP, "miss_count_1p")]
    [InlineData("miss_count", PlaySide.TwoP, "miss_count_2p")]
    [InlineData("ex_score", PlaySide.OneP, "ex_score_1p")]
    [InlineData("ex_score", PlaySide.TwoP, "ex_score_2p")]
    [InlineData("ex_score", PlaySide.Unknown, "ex_score_2p")] // Unknown は 2P 既定
    public void RecognitionRoiKeys_WithSide_BuildsKey(string baseKey, PlaySide side, string expected)
    {
        Assert.Equal(expected, RecognitionRoiKeys.WithSide(baseKey, side));
    }
}
