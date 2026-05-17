namespace InfTimestamper.Core.Models;

public enum GameId
{
    Infinitas
}

public static class GameIdExtensions
{
    public static string ToSerializedString(this GameId game) => game switch
    {
        GameId.Infinitas => "INFINITAS",
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, "Unknown game id"),
    };

    public static GameId ParseSerialized(string value) => value switch
    {
        "INFINITAS" => GameId.Infinitas,
        _ => throw new ArgumentException($"Unknown game id: {value}", nameof(value)),
    };

    public static bool TryParseSerialized(string value, out GameId game)
    {
        switch (value)
        {
            case "INFINITAS":
                game = GameId.Infinitas;
                return true;
            default:
                game = default;
                return false;
        }
    }
}
