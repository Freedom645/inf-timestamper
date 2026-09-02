using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Tests.Games;

public class FieldLabelsTests
{
    [Theory]
    [InlineData(GameId.Infinitas)]
    [InlineData(GameId.Popn)]
    [InlineData(GameId.Sdvx)]
    public void EveryIdentifier_HasALogicalName(GameId game)
    {
        foreach (var key in GameCatalog.Identifiers(game))
        {
            var label = FieldLabels.Of(key);
            Assert.False(string.IsNullOrWhiteSpace(label), $"{key} に論理名がありません。");
            Assert.NotEqual(key, label);
        }
    }

    [Fact]
    public void Of_UnknownKey_FallsBackToTheKeyItself()
    {
        Assert.Equal("unknown_key", FieldLabels.Of("unknown_key"));
    }

    [Fact]
    public void Choice_ShowsTheLogicalNameWithTheKey()
    {
        var choice = new IdentifierChoice(FieldKeys.DjLevel);

        Assert.Equal("dj_level", choice.Key);
        Assert.Equal("$dj_level", choice.Token);
        Assert.Equal("DJ レベル", choice.Label);
        Assert.Equal("DJ レベル ($dj_level)", choice.Display);
    }

    [Theory]
    [InlineData(GameId.Infinitas)]
    [InlineData(GameId.Popn)]
    [InlineData(GameId.Sdvx)]
    public void IdentifierChoices_MatchIdentifiersInOrder(GameId game)
    {
        Assert.Equal(
            GameCatalog.Identifiers(game),
            GameCatalog.IdentifierChoices(game).Select(c => c.Key));
    }
}
