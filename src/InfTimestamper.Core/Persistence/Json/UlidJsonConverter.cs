using System.Text.Json;
using System.Text.Json.Serialization;
using NUlid;

namespace InfTimestamper.Core.Persistence.Json;

public sealed class UlidJsonConverter : JsonConverter<Ulid>
{
    public override Ulid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw))
            throw new JsonException("Ulid string is empty");
        return Ulid.Parse(raw);
    }

    public override void Write(Utf8JsonWriter writer, Ulid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
