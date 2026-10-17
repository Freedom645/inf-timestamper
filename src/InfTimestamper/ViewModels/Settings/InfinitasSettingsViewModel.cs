using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// INFINITAS タブの ViewModel。
///
/// プレイ検知の取得元を Reflux（出力ディレクトリの監視。基底の
/// <see cref="DirectoryWatchSettingsViewModel.WatchDirectory"/>）と打鍵カウンタ（データ配信 WebSocket の購読。
/// ホスト + ポート）から選ぶ（issue #13）。選んでいない側の設定値も保持したまま保存する。
/// </summary>
public sealed class InfinitasSettingsViewModel : DirectoryWatchSettingsViewModel
{
    public const string BrowseDialogTitle = "Reflux 出力ディレクトリの選択";

    private PlaySourceId _playSource;
    private string _dakenCounterHost;
    private int _dakenCounterPort;

    public InfinitasSettingsViewModel(InfinitasSettings model)
        : this(model, null) { }

    public InfinitasSettingsViewModel(InfinitasSettings model, IDialogService? dialog)
        : base(
            GameId.Infinitas,
            (model ?? throw new ArgumentNullException(nameof(model))).TimestampFormat,
            model.RefluxDirectory,
            BrowseDialogTitle,
            dialog)
    {
        _playSource = PlaySourceIdExtensions.ParseInfinitasSource(model.PlaySource);
        _dakenCounterHost = string.IsNullOrWhiteSpace(model.DakenCounterHost)
            ? AppSettings.DefaultDakenCounterHost
            : model.DakenCounterHost;
        _dakenCounterPort = model.DakenCounterPort == 0 ? AppSettings.DefaultDakenCounterPort : model.DakenCounterPort;
    }

    /// <summary>プレイ検知の取得元。</summary>
    public PlaySourceId PlaySource
    {
        get => _playSource;
        set
        {
            if (!SetField(ref _playSource, value)) return;
            RaisePropertyChanged(nameof(IsRefluxSource));
            RaisePropertyChanged(nameof(IsDakenCounterSource));
        }
    }

    /// <summary>ラジオボタン用。Reflux を使うか。</summary>
    public bool IsRefluxSource
    {
        get => _playSource == PlaySourceId.Reflux;
        set { if (value) PlaySource = PlaySourceId.Reflux; }
    }

    /// <summary>ラジオボタン用。打鍵カウンタを使うか。</summary>
    public bool IsDakenCounterSource
    {
        get => _playSource == PlaySourceId.DakenCounter;
        set { if (value) PlaySource = PlaySourceId.DakenCounter; }
    }

    /// <summary>
    /// 打鍵カウンタのホスト。打鍵カウンタは 0.0.0.0 に bind するので、別 PC で動かしている場合はその IP を指定できる。
    /// </summary>
    public string DakenCounterHost
    {
        get => _dakenCounterHost;
        set
        {
            if (!SetField(ref _dakenCounterHost, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(IsDakenCounterHostValid));
            RaisePropertyChanged(nameof(IsDakenCounterLocalhost));
            RaisePropertyChanged(nameof(DakenCounterEndpoint));
        }
    }

    /// <summary>打鍵カウンタ側の <c>websocket_data_port</c>。</summary>
    public int DakenCounterPort
    {
        get => _dakenCounterPort;
        set
        {
            if (!SetField(ref _dakenCounterPort, value)) return;
            RaisePropertyChanged(nameof(IsDakenCounterPortValid));
            RaisePropertyChanged(nameof(DakenCounterEndpoint));
        }
    }

    public bool IsDakenCounterHostValid => HostValidation.IsIPv4OrLocalhost(_dakenCounterHost);

    public bool IsDakenCounterPortValid => _dakenCounterPort is > 0 and <= 65535;

    /// <summary>このPCの打鍵カウンタに繋ぐか。外すと任意のホストを入力できる。</summary>
    public bool IsDakenCounterLocalhost
    {
        get => HostValidation.IsLocalhost(_dakenCounterHost);
        set
        {
            if (value) DakenCounterHost = AppSettings.DefaultDakenCounterHost;
        }
    }

    /// <summary>接続先の表示用文字列（<c>ws://host:port</c>）。</summary>
    public string DakenCounterEndpoint => AppSettings.DakenCounterEndpoint(_dakenCounterHost, _dakenCounterPort);

    public InfinitasSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        RefluxDirectory = WatchDirectory,
        PlaySource = _playSource.ToInfinitasSerializedString(),
        DakenCounterHost = _dakenCounterHost,
        DakenCounterPort = _dakenCounterPort,
    };
}
