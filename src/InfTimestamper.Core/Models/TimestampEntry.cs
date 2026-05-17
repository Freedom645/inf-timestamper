using System.Text.Json;
using System.Text.Json.Serialization;
using NUlid;

namespace InfTimestamper.Core.Models;

public sealed class TimestampEntry
{
    [JsonPropertyOrder(0)]
    public Ulid Id { get; set; }

    [JsonPropertyOrder(1)]
    public DateTimeOffset PlayStartedAt { get; set; }

    [JsonPropertyOrder(2)]
    public Dictionary<string, JsonElement> Fields { get; set; } = new();

    public void SetField(string key, string value)
        => Fields[key] = JsonSerializer.SerializeToElement(value);

    public void SetField(string key, int value)
        => Fields[key] = JsonSerializer.SerializeToElement(value);

    public bool RemoveField(string key) => Fields.Remove(key);

    public bool TryGetFieldAsString(string key, out string value)
    {
        if (!Fields.TryGetValue(key, out var element))
        {
            value = string.Empty;
            return false;
        }

        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.ToString(),
        };
        return element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
    }
}
