using InfTimestamper.Core.Formatting;

namespace InfTimestamper.Core.Tests.Formatting;

public class FormatExpanderTests
{
    [Fact]
    public void Expand_SubstitutesAllKnownIdentifiers()
    {
        var fields = new Dictionary<string, string>
        {
            ["timestamp"] = "00:01:23",
            ["title"] = "Test Song",
            ["diff_s"] = "SPA",
            ["level"] = "11",
        };

        Assert.Equal(
            "[00:01:23] Test Song (SPA 11)",
            FormatExpander.Expand("[$timestamp] $title ($diff_s $level)", fields));
    }

    [Fact]
    public void Expand_MissingKey_ReplacedWithEmpty()
    {
        var fields = new Dictionary<string, string> { ["title"] = "X" };
        Assert.Equal("X - ", FormatExpander.Expand("$title - $missing", fields));
    }

    [Fact]
    public void Expand_EmptyFormat_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FormatExpander.Expand(string.Empty, new Dictionary<string, string>()));
    }

    [Fact]
    public void Expand_NullFields_StripsAllIdentifiers()
    {
        Assert.Equal("Hello ", FormatExpander.Expand("Hello $world", null));
    }

    [Fact]
    public void Expand_NullFormat_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FormatExpander.Expand(null, new Dictionary<string, string>()));
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(61, "00:01:01")]
    [InlineData(3661, "01:01:01")]
    [InlineData(-30, "-00:00:30")]
    [InlineData(-3661, "-01:01:01")]
    public void FormatTimestamp_HandlesPositiveAndNegative(int seconds, string expected)
    {
        Assert.Equal(expected, FormatExpander.FormatTimestamp(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void SupportedKeys_ContainsAllIdentifiersFromRequirements()
    {
        var keys = FormatExpander.SupportedKeys;
        Assert.Contains("timestamp", keys);
        Assert.Contains("title", keys);
        Assert.Contains("diff_l", keys);
        Assert.Contains("diff_s", keys);
        Assert.Contains("level", keys);
        Assert.Contains("miss_count", keys);
        Assert.Contains("ex_score", keys);
        Assert.Contains("dj_level", keys);
        Assert.Contains("lamp", keys);
    }
}
