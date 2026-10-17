using System.Text.Json.Serialization;
using InfTimestamper.Core.Games;
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

    /// <summary>打鍵カウンタのデータ配信 WebSocket の既定ホスト。</summary>
    public const string DefaultDakenCounterHost = "127.0.0.1";

    /// <summary>
    /// 打鍵カウンタの <c>websocket_data_port</c> の既定値。SDVX Helper と同じ値なので、
    /// 両方を常駐させる場合はどちらかのポートを変える必要がある。
    /// </summary>
    public const int DefaultDakenCounterPort = 8767;
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
            PlaySource = PlaySourceIdExtensions.RefluxSerialized,
            RefluxDirectory = string.Empty,
            DakenCounterHost = DefaultDakenCounterHost,
            DakenCounterPort = DefaultDakenCounterPort,
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
    /// ゲームのプレイ検知に使う取得元。INFINITAS だけ設定で Reflux / 打鍵カウンタを選べる（既定は Reflux）。
    /// </summary>
    public PlaySourceId PlaySourceFor(GameId game) => game switch
    {
        GameId.Popn => PlaySourceId.PopnTracker,
        GameId.Sdvx => PlaySourceId.SdvxHelper,
        _ => PlaySourceIdExtensions.ParseInfinitasSource(Infinitas?.PlaySource),
    };

    /// <summary>
    /// ゲーム検知の監視対象。Reflux / popn-lively-tracker は出力ディレクトリ、
    /// 打鍵カウンタはデータ配信 WebSocket の接続先、SDVX Helper はデータ配信 WebSocket の接続先 +
    /// 任意で SDVX Helper のフォルダ。未設定なら <see cref="WatchTarget.Empty"/>（検知しない）。
    /// </summary>
    public WatchTarget WatchTargetFor(GameId game) => PlaySourceFor(game) switch
    {
        PlaySourceId.PopnTracker => WatchTarget.ForDirectory(Popn?.TrackerDirectory),
        PlaySourceId.SdvxHelper => Sdvx is null
            ? WatchTarget.Empty
            : WatchTarget.ForEndpoint(SdvxHelperEndpoint(Sdvx.HelperHost, Sdvx.HelperPort), Sdvx.HelperDirectory),
        PlaySourceId.DakenCounter => Infinitas is null
            ? WatchTarget.Empty
            : WatchTarget.ForEndpoint(DakenCounterEndpoint(Infinitas.DakenCounterHost, Infinitas.DakenCounterPort)),
        _ => WatchTarget.ForDirectory(Infinitas?.RefluxDirectory),
    };

    /// <summary>SDVX Helper のデータ配信 WebSocket の接続先。ホスト未設定なら既定ホスト。</summary>
    public static string SdvxHelperEndpoint(string? host, int port)
        => $"ws://{(string.IsNullOrWhiteSpace(host) ? DefaultSdvxHelperHost : host.Trim())}:{port}";

    /// <summary>打鍵カウンタのデータ配信 WebSocket の接続先。ホスト未設定なら既定ホスト、ポート未設定なら既定ポート。</summary>
    public static string DakenCounterEndpoint(string? host, int port)
        => $"ws://{(string.IsNullOrWhiteSpace(host) ? DefaultDakenCounterHost : host.Trim())}:{(port == 0 ? DefaultDakenCounterPort : port)}";

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

    /// <summary>
    /// プレイ検知の取得元（<c>reflux</c> / <c>daken_counter</c>）。未知の値・欠損時は Reflux。
    /// v1.2.0 以前の settings.json には無いので、そのまま読めば従来どおり Reflux になる。
    /// </summary>
    [JsonPropertyOrder(2)]
    public string PlaySource { get; set; } = PlaySourceIdExtensions.RefluxSerialized;

    /// <summary>
    /// 打鍵カウンタのデータ配信 WebSocket のホスト。打鍵カウンタは 0.0.0.0 に bind するので、
    /// ゲーム PC で打鍵カウンタを動かし、配信 PC からその IP を指定する構成にもできる。
    /// </summary>
    [JsonPropertyOrder(3)]
    public string DakenCounterHost { get; set; } = AppSettings.DefaultDakenCounterHost;

    /// <summary>打鍵カウンタのデータ配信ポート（打鍵カウンタ側の <c>websocket_data_port</c>）。</summary>
    [JsonPropertyOrder(4)]
    public int DakenCounterPort { get; set; } = AppSettings.DefaultDakenCounterPort;
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

    /// <summary>
    /// SDVX Helper のフォルダ（<c>sdvx_helper.exe</c> があり、直下に <c>log/</c> が作られる場所）。任意。
    /// 指定すると <c>log/sdvx_helper.log</c> の画面遷移ログを監視し、リトライや曲決定画面の
    /// 取りこぼしがあってもプレイ開始を記録できる。
    /// </summary>
    [JsonPropertyOrder(3)]
    public string HelperDirectory { get; set; } = string.Empty;
}
