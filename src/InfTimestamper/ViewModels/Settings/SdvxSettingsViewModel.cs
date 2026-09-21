using InfTimestamper.Core.Models;
using InfTimestamper.Core.Sdvx;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// SOUND VOLTEX タブの ViewModel。
///
/// SDVX Helper は v2 系でファイル出力をやめてデータ配信 WebSocket へ移行しているため、
/// 接続先（ホスト + ポート）を設定させる。加えて SDVX Helper のフォルダ（任意）を指定すると
/// <c>log/sdvx_helper.log</c> の画面遷移を監視でき、リトライや曲決定画面の取りこぼしがあっても
/// プレイ開始を記録できる（基底の <see cref="DirectoryWatchSettingsViewModel.WatchDirectory"/> がその役）。
/// </summary>
public sealed class SdvxSettingsViewModel : DirectoryWatchSettingsViewModel
{
    public const string BrowseDialogTitle = "SDVX Helper のフォルダの選択";

    private string _helperHost;
    private int _helperPort;

    public SdvxSettingsViewModel(SdvxSettings model)
        : this(model, null) { }

    public SdvxSettingsViewModel(SdvxSettings model, IDialogService? dialog)
        : base(
            GameId.Sdvx,
            (model ?? throw new ArgumentNullException(nameof(model))).TimestampFormat,
            model.HelperDirectory,
            BrowseDialogTitle,
            dialog)
    {
        _helperHost = string.IsNullOrWhiteSpace(model.HelperHost)
            ? AppSettings.DefaultSdvxHelperHost
            : model.HelperHost;
        _helperPort = model.HelperPort == 0 ? AppSettings.DefaultSdvxHelperPort : model.HelperPort;
    }

    /// <summary>
    /// SDVX Helper のホスト。SDVX Helper 自身は localhost にしか bind しないため、
    /// 通常は既定値のままでよい（ポートフォワード等を挟む場合のために設定できるようにしてある）。
    /// </summary>
    public string HelperHost
    {
        get => _helperHost;
        set
        {
            if (!SetField(ref _helperHost, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(IsHelperHostValid));
            RaisePropertyChanged(nameof(IsLocalhost));
            RaisePropertyChanged(nameof(Endpoint));
        }
    }

    /// <summary>SDVX Helper 側の <c>websocket_data_port</c>。</summary>
    public int HelperPort
    {
        get => _helperPort;
        set
        {
            if (!SetField(ref _helperPort, value)) return;
            RaisePropertyChanged(nameof(IsHelperPortValid));
            RaisePropertyChanged(nameof(Endpoint));
        }
    }

    public bool IsHelperHostValid => HostValidation.IsIPv4OrLocalhost(_helperHost);

    public bool IsHelperPortValid => _helperPort is > 0 and <= 65535;

    /// <summary>ローカル PC の SDVX Helper に繋ぐか。外すと任意のホストを入力できる。</summary>
    public bool IsLocalhost
    {
        get => HostValidation.IsLocalhost(_helperHost);
        set
        {
            if (value) HelperHost = AppSettings.DefaultSdvxHelperHost;
        }
    }

    /// <summary>接続先の表示用文字列（<c>ws://host:port</c>）。</summary>
    public string Endpoint => AppSettings.SdvxHelperEndpoint(_helperHost, _helperPort);

    /// <summary>監視するログファイルの表示用パス。フォルダ未指定なら空。</summary>
    public string LogPath
        => string.IsNullOrWhiteSpace(WatchDirectory)
            ? string.Empty
            : SdvxHelperLogTail.ResolveLogPath(WatchDirectory.Trim());

    protected override void OnWatchDirectoryChanged() => RaisePropertyChanged(nameof(LogPath));

    public SdvxSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        HelperHost = _helperHost,
        HelperPort = _helperPort,
        HelperDirectory = WatchDirectory,
    };
}
