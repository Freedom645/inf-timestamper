using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class ObsSettingsViewModel : ObservableBase
{
    private readonly IObsConnectionTester? _tester;
    private readonly IDialogService? _dialog;

    private string _host = AppSettings.DefaultObsHost;
    private int _port = AppSettings.DefaultObsPort;
    private string _password = string.Empty;
    private bool _isTesting;

    public ObsSettingsViewModel(ObsConnectionSettings model)
        : this(model, null, null) { }

    public ObsSettingsViewModel(
        ObsConnectionSettings model,
        IObsConnectionTester? tester,
        IDialogService? dialog)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _host = string.IsNullOrEmpty(model.Host) ? AppSettings.DefaultObsHost : model.Host;
        _port = model.Port == 0 ? AppSettings.DefaultObsPort : model.Port;
        _password = model.Password ?? string.Empty;
        _tester = tester;
        _dialog = dialog;

        TestConnectionCommand = new RelayCommand(
            ExecuteTestConnection,
            () => _tester is not null && _dialog is not null && !_isTesting);
    }

    public string Host
    {
        get => _host;
        set
        {
            if (!SetField(ref _host, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(IsLocalhost));
        }
    }

    public int Port
    {
        get => _port;
        set => SetField(ref _port, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value ?? string.Empty);
    }

    public bool IsLocalhost
    {
        get => string.Equals(_host, "127.0.0.1", StringComparison.Ordinal)
               || string.Equals(_host, "localhost", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value) Host = "127.0.0.1";
        }
    }

    public bool IsTesting
    {
        get => _isTesting;
        private set
        {
            if (SetField(ref _isTesting, value))
                TestConnectionCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand TestConnectionCommand { get; }

    public ObsConnectionSettings ToModel() => new()
    {
        Host = _host,
        Port = _port,
        Password = _password,
    };

    public ObsConnectionOptions ToOptions() => new(_host, _port, _password);

    private async void ExecuteTestConnection()
    {
        if (_tester is null || _dialog is null) return;
        IsTesting = true;
        try
        {
            var result = await _tester.TestAsync(ToOptions(), CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                _dialog.ShowInfo("接続テスト",
                    $"接続に成功しました。\n\nOBS バージョン: {result.ObsVersion}\n現在のシーン: {result.CurrentScene}");
            }
            else
            {
                _dialog.ShowError("接続テスト", "接続に失敗しました: " + result.Message);
            }
        }
        finally
        {
            IsTesting = false;
        }
    }
}
