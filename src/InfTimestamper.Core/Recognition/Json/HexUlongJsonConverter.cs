using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Recognition.Json;

public sealed class HexUlongJsonConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetUInt64();

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("ハッシュは文字列または数値で指定してください。");

        var s = reader.GetString() ?? throw new JsonException("空のハッシュ値です。");
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];

        if (!ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new JsonException($"ハッシュ値を 16 進として解釈できません: {s}");

        return value;
    }

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("0x" + value.ToString("x16", CultureInfo.InvariantCulture));
    }
}
