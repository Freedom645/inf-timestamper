using InfTimestamper.Core.Updates;

namespace InfTimestamper.Core.Tests.Updates;

public class VersionComparerTests
{
    [Theory]
    [InlineData("v1.0.0", 1, 0, 0, null)]
    [InlineData("V1.2.3", 1, 2, 3, null)]
    [InlineData("1.0.0", 1, 0, 0, null)]
    [InlineData("v1.0.0-beta", 1, 0, 0, "beta")]
    [InlineData("v2.5.0-rc1", 2, 5, 0, "rc1")]
    [InlineData("v1.2.0-alpha.1", 1, 2, 0, "alpha.1")]
    [InlineData("1.2.0-alpha.1+abc123", 1, 2, 0, "alpha.1")]   // ビルドメタデータは無視
    [InlineData("v0.6.1", 0, 6, 1, null)]
    [InlineData("v1.2", 1, 2, 0, null)]
    public void TryParseTag_ValidTags(string tag, int major, int minor, int patch, string? prerelease)
    {
        Assert.True(VersionComparer.TryParseTag(tag, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(prerelease, version.Prerelease);
        Assert.Equal(prerelease is not null, version.IsPrerelease);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("v")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
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
    // プレリリース: 正式版 > プレリリース、α の連番は数値比較
    [InlineData("v1.2.0", "1.2.0-alpha.1", true)]
    [InlineData("v1.2.0-alpha.1", "1.2.0", false)]
    [InlineData("v1.2.0-alpha.2", "1.2.0-alpha.1", true)]
    [InlineData("v1.2.0-alpha.10", "1.2.0-alpha.9", true)]
    [InlineData("v1.2.0-alpha.1", "1.2.0-alpha.1", false)]
    [InlineData("v1.2.0-beta.1", "1.2.0-alpha.9", true)]
    [InlineData("v1.2.0-alpha.1", "1.2.0-alpha", true)]
    [InlineData("v1.2.0-alpha.1", "1.1.0", true)]
    [InlineData("v1.1.0", "1.2.0-alpha.1", false)]
    public void IsNewer_ComparesCorrectly(string remote, string current, bool expected)
    {
        Assert.True(SemanticVersion.TryParse(current, out var currentVersion));
        Assert.Equal(expected, VersionComparer.IsNewer(remote, currentVersion));
    }

    [Theory]
    [InlineData(1, 2, 0, null, "1.2.0")]
    [InlineData(1, 2, 0, "alpha.1", "1.2.0-alpha.1")]
    public void SemanticVersion_ToString_RoundTrips(int major, int minor, int patch, string? pre, string expected)
    {
        var version = new SemanticVersion(major, minor, patch, pre);
        Assert.Equal(expected, version.ToString());
        Assert.True(SemanticVersion.TryParse(version.ToString(), out var parsed));
        Assert.Equal(version, parsed);
    }

    [Fact]
    public void SemanticVersion_FromAssembly_ReadsTheInformationalVersion()
    {
        // テストアセンブリ自身。csproj の <Version> 未指定なら SDK 既定の 1.0.0
        var version = SemanticVersion.FromAssembly(typeof(VersionComparerTests).Assembly);
        Assert.True(version.CompareTo(SemanticVersion.Zero) > 0);
    }
}
