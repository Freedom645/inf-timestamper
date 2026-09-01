using System.Text.Json.Serialization;
using InfTimestamper.Core.Models;

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

    [JsonPropertyOrder(4)]
    public PopnSettings Popn { get; set; } = new();

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
        Popn = new PopnSettings
        {
            TimestampFormat = DefaultTimestampFormat,
            TrackerDirectory = string.Empty,
        },
    };

    /// <summary>ゲームごとのタイムスタンプフォーマット。未設定なら既定フォーマット。</summary>
    public string TimestampFormatFor(GameId game)
    {
        var format = game switch
        {
            GameId.Popn => Popn?.TimestampFormat,
            _ => Infinitas?.TimestampFormat,
        };
        return string.IsNullOrEmpty(format) ? DefaultTimestampFormat : format;
    }

    /// <summary>ゲーム検知に使う外部ツールの出力ディレクトリ。未設定なら空文字列（検知しない）。</summary>
    public string WatchDirectoryFor(GameId game) => (game switch
    {
        GameId.Popn => Popn?.TrackerDirectory,
        _ => Infinitas?.RefluxDirectory,
    }) ?? string.Empty;

    /// <summary>永続化された選択ゲームを解釈する。未知の値・欠損時は INFINITAS。</summary>
    public GameId ResolveSelectedGame()
    {
        var raw = General?.SelectedGame;
        return !string.IsNullOrEmpty(raw) && GameIdExtensions.TryParseSerialized(raw, out var game)
            ? game
            : GameId.Infinitas;
    }

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

    /// <summary>
    /// 記録対象のゲーム（シリアライズ表記）。メインウィンドウのゲーム選択を次回起動へ引き継ぐために持つ。
    /// 未知の値・欠損時は INFINITAS 扱い。
    /// </summary>
    [JsonPropertyOrder(4)]
    public string SelectedGame { get; set; } = GameIdExtensions.InfinitasSerialized;
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

public sealed class PopnSettings
{
    [JsonPropertyOrder(0)]
    public string TimestampFormat { get; set; } = AppSettings.DefaultTimestampFormat;

    /// <summary>popn-lively-tracker の出力ディレクトリ（state.txt / result.json を含む）。</summary>
    [JsonPropertyOrder(1)]
    public string TrackerDirectory { get; set; } = string.Empty;
}
