using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Models;

public sealed class AppInfo
{
    public const string DefaultName = "inf-timestamper";

    [JsonPropertyOrder(0)]
    public string Name { get; set; } = DefaultName;

    [JsonPropertyOrder(1)]
    public string Version { get; set; } = "1.0.0";
}
