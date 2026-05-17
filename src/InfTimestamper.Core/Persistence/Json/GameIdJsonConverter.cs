using System.Text.Json;
using System.Text.Json.Serialization;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Persistence.Json;

public sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw))
            throw new JsonException("Game id is empty");
        if (!GameIdExtensions.TryParseSerialized(raw, out var game))
            throw new UnknownGameException(raw);
        return game;
    }

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToSerializedString());
    }
}

public sealed class UnknownGameException : Exception
{
    public string GameValue { get; }

    public UnknownGameException(string gameValue)
        : base($"対応していないゲーム識別子です: '{gameValue}'")
    {
        GameValue = gameValue;
    }
}
