using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// SOUND VOLTEX タブの ViewModel。
///
/// SDVX Helper は v2 系でファイル出力をやめてデータ配信 WebSocket へ移行しているため、
/// 他ゲームの「出力ディレクトリ」ではなく接続先ポートを設定させる。
/// ホストは SDVX Helper 側が localhost にしか bind しないので固定。
/// </summary>
public sealed class SdvxSettingsViewModel : GameFormatSettingsViewModel
{
    private int _helperPort;

    public SdvxSettingsViewModel(SdvxSettings model)
        : base(GameId.Sdvx, (model ?? throw new ArgumentNullException(nameof(model))).TimestampFormat)
    {
        _helperPort = model.HelperPort == 0 ? AppSettings.DefaultSdvxHelperPort : model.HelperPort;
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

    public bool IsHelperPortValid => _helperPort is > 0 and <= 65535;

    public override string WatchTarget => AppSettings.SdvxHelperEndpoint(_helperPort);

    public SdvxSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        HelperPort = _helperPort,
    };
}
