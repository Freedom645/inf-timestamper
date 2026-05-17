using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Models;

public sealed class StreamInfo
{
    [JsonPropertyOrder(0)]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyOrder(1)]
    public DateTimeOffset? EndedAt { get; set; }
}
