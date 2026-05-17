using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Models;

public sealed class StreamRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyOrder(1)]
    public AppInfo App { get; set; } = new();

    [JsonPropertyOrder(2)]
    public GameId Game { get; set; } = GameId.Infinitas;

    [JsonPropertyOrder(3)]
    public StreamInfo Stream { get; set; } = new();

    [JsonPropertyOrder(4)]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyOrder(5)]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyOrder(6)]
    public List<TimestampEntry> Timestamps { get; set; } = new();
}
