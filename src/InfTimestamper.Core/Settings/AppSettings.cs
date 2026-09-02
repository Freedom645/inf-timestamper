using System.Text.Json.Serialization;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Settings;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultTimestampFormat = "$timestamp $title [$diff_s $level]";
    public const string DefaultObsHost = "127.0.0.1";
    public const int DefaultObsPort = 4455;

    /// <summary>SDVX Helper のデータ配信 WebSocket の既定ホスト。</summary>
    public const string DefaultSdvxHelperHost = "127.0.0.1";

    /// <summary>SDVX Helper の <c>websocket_data_port</c> の既定値。</summary>
    public const int DefaultSdvxHelperPort = 8767;
    public const string DefaultStreamStartRowLabel = "配信開始";

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

    [JsonPropertyOrder(5)]
    public SdvxSettings Sdvx { get; set; } = new();

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
        Sdvx = new SdvxSettings
        {
            TimestampFormat = DefaultTimestampFormat,
            HelperHost = DefaultSdvxHelperHost,
            HelperPort = DefaultSdvxHelperPort,
        },
    };

    /// <summary>ゲームごとのタイムスタンプフォーマット。未設定なら既定フォーマット。</summary>
    public string TimestampFormatFor(GameId game)
    {
        var format = game switch
        {
            GameId.Popn => Popn?.TimestampFormat,
            GameId.Sdvx => Sdvx?.TimestampFormat,
            _ => Infinitas?.TimestampFormat,
        };
        return string.IsNullOrEmpty(format) ? DefaultTimestampFormat : format;
    }

    /// <summary>
    /// ゲーム検知の監視対象。INFINITAS / pop'n music は外部ツールの出力ディレクトリ、
    /// SOUND VOLTEX は SDVX Helper のデータ配信 WebSocket の接続先。
    /// 未設定なら空文字列（検知しない）。
    /// </summary>
    public string WatchTargetFor(GameId game) => (game switch
    {
        GameId.Popn => Popn?.TrackerDirectory,
        GameId.Sdvx => Sdvx is null ? null : SdvxHelperEndpoint(Sdvx.HelperHost, Sdvx.HelperPort),
        _ => Infinitas?.RefluxDirectory,
    }) ?? string.Empty;

    /// <summary>SDVX Helper のデータ配信 WebSocket の接続先。ホスト未設定なら既定ホスト。</summary>
    public static string SdvxHelperEndpoint(string? host, int port)
        => $"ws://{(string.IsNullOrWhiteSpace(host) ? DefaultSdvxHelperHost : host.Trim())}:{port}";

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

    /// <summary>
    /// タイムスタンプリストとコピー結果の先頭に「00:00:00 配信開始」行を入れるか。
    /// YouTube のチャプタ機能は先頭が 0 秒のチャプタであることを要求するため既定で ON。
    /// </summary>
    [JsonPropertyOrder(5)]
    public bool IncludeStreamStartRow { get; set; } = true;

    /// <summary>配信開始行の見出し文言。空の場合は既定値を使う。</summary>
    [JsonPropertyOrder(6)]
    public string StreamStartRowLabel { get; set; } = AppSettings.DefaultStreamStartRowLabel;
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

public sealed class SdvxSettings
{
    [JsonPropertyOrder(0)]
    public string TimestampFormat { get; set; } = AppSettings.DefaultTimestampFormat;

    /// <summary>
    /// SDVX Helper のデータ配信 WebSocket のホスト。
    /// SDVX Helper 自身は localhost にしか bind しないため、通常は既定値のままでよい
    /// （ポートフォワード等を挟む場合のために設定できるようにしてある）。
    /// </summary>
    [JsonPropertyOrder(1)]
    public string HelperHost { get; set; } = AppSettings.DefaultSdvxHelperHost;

    /// <summary>SDVX Helper のデータ配信ポート（SDVX Helper 側の <c>websocket_data_port</c>）。</summary>
    [JsonPropertyOrder(2)]
    public int HelperPort { get; set; } = AppSettings.DefaultSdvxHelperPort;
}
