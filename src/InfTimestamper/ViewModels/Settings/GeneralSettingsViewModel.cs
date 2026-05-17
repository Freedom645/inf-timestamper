using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

public sealed class GeneralSettingsViewModel : ObservableBase
{
    private bool _autoUpdateCheck;
    private string _backupDirectory = string.Empty;
    private bool _confirmOnReset;
    private bool _confirmOnExit;

    public GeneralSettingsViewModel(GeneralSettings model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _autoUpdateCheck = model.AutoUpdateCheck;
        _backupDirectory = model.BackupDirectory ?? string.Empty;
        _confirmOnReset = model.ConfirmOnReset;
        _confirmOnExit = model.ConfirmOnExit;
    }

    public bool AutoUpdateCheck
    {
        get => _autoUpdateCheck;
        set => SetField(ref _autoUpdateCheck, value);
    }

    public string BackupDirectory
    {
        get => _backupDirectory;
        set => SetField(ref _backupDirectory, value ?? string.Empty);
    }

    public bool ConfirmOnReset
    {
        get => _confirmOnReset;
        set => SetField(ref _confirmOnReset, value);
    }

    public bool ConfirmOnExit
    {
        get => _confirmOnExit;
        set => SetField(ref _confirmOnExit, value);
    }

    public GeneralSettings ToModel() => new()
    {
        AutoUpdateCheck = _autoUpdateCheck,
        BackupDirectory = _backupDirectory,
        ConfirmOnReset = _confirmOnReset,
        ConfirmOnExit = _confirmOnExit,
    };
}
