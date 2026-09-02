using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// SOUND VOLTEX タブの ViewModel。
///
/// SDVX Helper は v2 系でファイル出力をやめてデータ配信 WebSocket へ移行しているため、
/// 他ゲームの「出力ディレクトリ」ではなく接続先（ホスト + ポート）を設定させる。
/// </summary>
public sealed class SdvxSettingsViewModel : GameFormatSettingsViewModel
{
    private string _helperHost;
    private int _helperPort;

    public SdvxSettingsViewModel(SdvxSettings model)
        : base(GameId.Sdvx, (model ?? throw new ArgumentNullException(nameof(model))).TimestampFormat)
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
            RaisePropertyChanged(nameof(WatchTarget));
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
            RaisePropertyChanged(nameof(WatchTarget));
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

    public override string WatchTarget => AppSettings.SdvxHelperEndpoint(_helperHost, _helperPort);

    public SdvxSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        HelperHost = _helperHost,
        HelperPort = _helperPort,
    };
}
