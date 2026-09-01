using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Models;

public sealed class AppInfo
{
    public const string DefaultName = "inf-timestamper";
    public const string DefaultVersion = "1.0.0";

    [JsonPropertyOrder(0)]
    public string Name { get; set; } = DefaultName;

    /// <summary>出力時のアプリバージョン。保存時に <see cref="JsonRecordStore"/> が実行アセンブリの値で上書きする。</summary>
    [JsonPropertyOrder(1)]
    public string Version { get; set; } = DefaultVersion;
}
