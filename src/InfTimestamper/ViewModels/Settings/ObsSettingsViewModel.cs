using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

public sealed class ObsSettingsViewModel : ObservableBase
{
    private string _host = AppSettings.DefaultObsHost;
    private int _port = AppSettings.DefaultObsPort;
    private string _password = string.Empty;

    public ObsSettingsViewModel(ObsConnectionSettings model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _host = string.IsNullOrEmpty(model.Host) ? AppSettings.DefaultObsHost : model.Host;
        _port = model.Port == 0 ? AppSettings.DefaultObsPort : model.Port;
        _password = model.Password ?? string.Empty;
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

    public ObsConnectionSettings ToModel() => new()
    {
        Host = _host,
        Port = _port,
        Password = _password,
    };
}
