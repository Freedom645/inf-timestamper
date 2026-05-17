using InfTimestamper.Core.Updates;

namespace InfTimestamper.Core.Tests.Updates;

public class VersionComparerTests
{
    [Theory]
    [InlineData("v1.0.0", 1, 0, 0)]
    [InlineData("V1.2.3", 1, 2, 3)]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("v1.0.0-beta", 1, 0, 0)]
    [InlineData("v2.5.0-rc1", 2, 5, 0)]
    [InlineData("v0.6.1", 0, 6, 1)]
    public void TryParseTag_ValidTags(string tag, int major, int minor, int build)
    {
        Assert.True(VersionComparer.TryParseTag(tag, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(build, version.Build);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("v")]
    public void TryParseTag_InvalidTags(string? tag)
    {
        Assert.False(VersionComparer.TryParseTag(tag, out _));
    }

    [Theory]
    [InlineData("v1.0.1", "1.0.0", true)]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("v2.0.0", "1.99.99", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "1.0.1", false)]
    [InlineData("invalid", "1.0.0", false)]
    public void IsNewer_ComparesCorrectly(string remote, string current, bool expected)
    {
        var currentVersion = Version.Parse(current);
        Assert.Equal(expected, VersionComparer.IsNewer(remote, currentVersion));
    }
}
