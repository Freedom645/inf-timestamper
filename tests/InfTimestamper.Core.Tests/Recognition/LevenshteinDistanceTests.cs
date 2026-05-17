using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class LevenshteinDistanceTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("flaw", "lawn", 2)]
    [InlineData("GIGARAID", "GIGARAIDX", 1)]
    [InlineData("GIGARAID", "GIGARAI0", 1)] // 1 char substitution
    public void Compute_ReturnsExpectedDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, LevenshteinDistance.Compute(a, b));
    }

    [Fact]
    public void Compute_IsSymmetric()
    {
        Assert.Equal(
            LevenshteinDistance.Compute("kitten", "sitting"),
            LevenshteinDistance.Compute("sitting", "kitten"));
    }
}
