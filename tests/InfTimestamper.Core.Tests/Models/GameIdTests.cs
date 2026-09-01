using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Tests.Models;

public class GameIdTests
{
    [Theory]
    [InlineData(GameId.Infinitas, "INFINITAS")]
    [InlineData(GameId.Popn, "POPN")]
    public void ToSerializedString_ReturnsExpectedValue(GameId game, string expected)
    {
        Assert.Equal(expected, game.ToSerializedString());
    }

    [Theory]
    [InlineData("INFINITAS", GameId.Infinitas)]
    [InlineData("POPN", GameId.Popn)]
    public void ParseSerialized_RoundTripsKnownValue(string raw, GameId expected)
    {
        Assert.Equal(expected, GameIdExtensions.ParseSerialized(raw));
    }

    [Fact]
    public void EveryGameId_HasASerializedForm()
    {
        foreach (var game in Enum.GetValues<GameId>())
        {
            var serialized = game.ToSerializedString();
            Assert.True(GameIdExtensions.TryParseSerialized(serialized, out var parsed));
            Assert.Equal(game, parsed);
        }
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
