using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Tests.Models;

public class GameIdTests
{
    [Fact]
    public void ToSerializedString_ReturnsExpectedValue()
    {
        Assert.Equal("INFINITAS", GameId.Infinitas.ToSerializedString());
    }

    [Fact]
    public void ParseSerialized_RoundTripsKnownValue()
    {
        Assert.Equal(GameId.Infinitas, GameIdExtensions.ParseSerialized("INFINITAS"));
    }

    [Fact]
    public void ParseSerialized_ThrowsForUnknownValue()
    {
        Assert.Throws<ArgumentException>(() => GameIdExtensions.ParseSerialized("UNKNOWN"));
    }

    [Fact]
    public void TryParseSerialized_ReturnsFalseForUnknownValue()
    {
        Assert.False(GameIdExtensions.TryParseSerialized("UNKNOWN", out _));
    }
}
