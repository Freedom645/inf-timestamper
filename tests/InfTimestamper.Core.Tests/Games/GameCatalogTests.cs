using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Tests.Games;

public class GameCatalogTests
{
    [Fact]
    public void AllGames_CoversEveryGameId()
    {
        Assert.Equal(Enum.GetValues<GameId>().Length, GameCatalog.AllGames.Count);
        foreach (var game in Enum.GetValues<GameId>())
            Assert.Contains(game, GameCatalog.AllGames);
    }

    [Fact]
    public void PopnIdentifiers_MatchTheHybridDecision()
    {
        var keys = GameCatalog.Identifiers(GameId.Popn);

        // 共通で流用するもの
        Assert.Contains(FieldKeys.Timestamp, keys);
        Assert.Contains(FieldKeys.Title, keys);
        Assert.Contains(FieldKeys.Level, keys);
        Assert.Contains(FieldKeys.DiffLong, keys);
        Assert.Contains(FieldKeys.DiffShort, keys);

        // pop'n 固有に新設したもの
        Assert.Contains(FieldKeys.Rank, keys);
        Assert.Contains(FieldKeys.Medal, keys);
        Assert.Contains(FieldKeys.Score, keys);
        Assert.Contains(FieldKeys.Bad, keys);

        // INFINITAS 固有の識別子は含まない
        Assert.DoesNotContain(FieldKeys.DjLevel, keys);
        Assert.DoesNotContain(FieldKeys.Lamp, keys);
        Assert.DoesNotContain(FieldKeys.ExScore, keys);
        Assert.DoesNotContain(FieldKeys.MissCount, keys);
    }

    [Fact]
    public void InfinitasIdentifiers_DoNotIncludePopnOnlyKeys()
    {
        var keys = GameCatalog.Identifiers(GameId.Infinitas);

        Assert.DoesNotContain(FieldKeys.Rank, keys);
        Assert.DoesNotContain(FieldKeys.Medal, keys);
        Assert.DoesNotContain(FieldKeys.Score, keys);
        Assert.DoesNotContain(FieldKeys.Bad, keys);
    }

    [Theory]
    [InlineData(GameId.Infinitas)]
    [InlineData(GameId.Popn)]
    public void PreviewFields_CoverEveryIdentifier(GameId game)
    {
        var preview = GameCatalog.PreviewFields(game);
        foreach (var key in GameCatalog.Identifiers(game))
            Assert.True(preview.ContainsKey(key), $"{game} のプレビューに {key} がありません。");
    }

    [Theory]
    [InlineData(GameId.Infinitas)]
    [InlineData(GameId.Popn)]
    public void PreviewFields_ExpandEveryIdentifierToNonEmpty(GameId game)
    {
        foreach (var key in GameCatalog.Identifiers(game))
        {
            var expanded = FormatExpander.Expand("$" + key, GameCatalog.PreviewFields(game));
            Assert.False(string.IsNullOrEmpty(expanded), $"{game} の ${key} が空に展開されました。");
        }
    }

    [Theory]
    [InlineData(GameId.Infinitas)]
    [InlineData(GameId.Popn)]
    public void DisplayNameAndToolName_AreNotEmpty(GameId game)
    {
        Assert.False(string.IsNullOrWhiteSpace(GameCatalog.DisplayName(game)));
        Assert.False(string.IsNullOrWhiteSpace(GameCatalog.WatcherToolName(game)));
    }
}
