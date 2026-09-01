namespace InfTimestamper.Core.Models;

public enum GameId
{
    Infinitas,
    Popn,
}

public static class GameIdExtensions
{
    public const string InfinitasSerialized = "INFINITAS";
    public const string PopnSerialized = "POPN";

    public static string ToSerializedString(this GameId game) => game switch
    {
        GameId.Infinitas => InfinitasSerialized,
        GameId.Popn => PopnSerialized,
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, "Unknown game id"),
    };

    public static GameId ParseSerialized(string value)
        => TryParseSerialized(value, out var game)
            ? game
            : throw new ArgumentException($"Unknown game id: {value}", nameof(value));

    public static bool TryParseSerialized(string value, out GameId game)
    {
        switch (value)
        {
            case InfinitasSerialized:
                game = GameId.Infinitas;
                return true;
            case PopnSerialized:
                game = GameId.Popn;
                return true;
            default:
                game = default;
                return false;
        }
    }
}
