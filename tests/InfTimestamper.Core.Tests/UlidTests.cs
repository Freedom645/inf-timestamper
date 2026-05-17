using NUlid;

namespace InfTimestamper.Core.Tests;

public class UlidTests
{
    [Fact]
    public void Ulid_IsComparable_AndOrderedByTime()
    {
        var first = Ulid.NewUlid();
        Thread.Sleep(2);
        var second = Ulid.NewUlid();
        Assert.True(first.CompareTo(second) < 0);
    }

    [Fact]
    public void Ulid_StringRepresentationIs26CrockfordChars()
    {
        var ulid = Ulid.NewUlid();
        var str = ulid.ToString();
        Assert.Equal(26, str.Length);
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]+$", str);
    }
}
