using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Recognition.Json;

public sealed class RoiJsonConverter : JsonConverter<Roi>
{
    public override Roi Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("ROI は [x, y, w, h] の配列で指定してください。");

        var values = new List<int>(4);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException("ROI の要素は整数である必要があります。");
            values.Add(reader.GetInt32());
        }

        if (values.Count != 4)
            throw new JsonException("ROI は 4 要素 [x, y, w, h] で指定してください。");

        return new Roi(values[0], values[1], values[2], values[3]);
    }

    public override void Write(Utf8JsonWriter writer, Roi value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Width);
        writer.WriteNumberValue(value.Height);
        writer.WriteEndArray();
    }
}
