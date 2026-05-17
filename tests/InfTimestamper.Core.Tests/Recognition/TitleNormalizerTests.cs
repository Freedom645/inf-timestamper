using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class TitleNormalizerTests
{
    [Theory]
    [InlineData("GIGA RAID", "GIGARAID")]
    [InlineData("giga raid", "GIGARAID")]
    [InlineData("ＧＩＧＡ ＲＡＩＤ", "GIGARAID")]
    [InlineData("5.1.1.", "511")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Hello, World!", "HELLOWORLD")]
    [InlineData("a/b\\c:d_e", "ABCDE")]
    public void Normalize_RemovesPunctuationAndUppercases(string input, string expected)
    {
        var actual = TitleNormalizer.Normalize(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_PreservesHiraganaKatakanaCjk()
    {
        Assert.Equal("ひらがな", TitleNormalizer.Normalize("ひらがな"));
        Assert.Equal("カタカナ", TitleNormalizer.Normalize("カタカナ"));
        Assert.Equal("漢字", TitleNormalizer.Normalize("漢字"));
        Assert.Equal("ABCあいう", TitleNormalizer.Normalize("abc あいう"));
    }
}
