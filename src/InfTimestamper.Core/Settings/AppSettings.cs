using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Settings;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultTimestampFormat = "$timestamp $title [$diff_s $level]";
    public const string DefaultObsHost = "127.0.0.1";
    public const int DefaultObsPort = 4455;

    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyOrder(1)]
    public GeneralSettings General { get; set; } = new();

    [JsonPropertyOrder(2)]
    public ObsConnectionSettings Obs { get; set; } = new();

    [JsonPropertyOrder(3)]
    public InfinitasSettings Infinitas { get; set; } = new();

    public static AppSettings CreateDefault() => new()
    {
        General = new GeneralSettings
        {
            BackupDirectory = DefaultBackupDirectory(),
        },
        Obs = new ObsConnectionSettings
        {
            Host = DefaultObsHost,
            Port = DefaultObsPort,
            Password = string.Empty,
        },
        Infinitas = new InfinitasSettings
        {
            TimestampFormat = DefaultTimestampFormat,
            RefluxDirectory = string.Empty,
        },
    };

    public static string DefaultBackupDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "inf-timestamper",
            "backups");
}

public sealed class GeneralSettings
{
    [JsonPropertyOrder(0)]
    public bool AutoUpdateCheck { get; set; } = true;

    [JsonPropertyOrder(1)]
    public string BackupDirectory { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public bool ConfirmOnReset { get; set; } = true;

    [JsonPropertyOrder(3)]
    public bool ConfirmOnExit { get; set; } = true;
}

public sealed class ObsConnectionSettings
{
    [JsonPropertyOrder(0)]
    public string Host { get; set; } = AppSettings.DefaultObsHost;

    [JsonPropertyOrder(1)]
    public int Port { get; set; } = AppSettings.DefaultObsPort;

    [JsonPropertyOrder(2)]
    public string Password { get; set; } = string.Empty;
}

public sealed class InfinitasSettings
{
    [JsonPropertyOrder(0)]
    public string TimestampFormat { get; set; } = AppSettings.DefaultTimestampFormat;

    /// <summary>Reflux の出力ディレクトリ（playstate.txt / title.txt / level.txt / latest.json を含む）。</summary>
    [JsonPropertyOrder(1)]
    public string RefluxDirectory { get; set; } = string.Empty;
}
