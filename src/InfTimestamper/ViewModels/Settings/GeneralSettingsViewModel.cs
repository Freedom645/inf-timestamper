using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class GeneralSettingsViewModel : ObservableBase
{
    private readonly IDialogService? _dialog;

    private bool _autoUpdateCheck;
    private string _backupDirectory = string.Empty;
    private bool _confirmOnReset;
    private bool _confirmOnExit;

    public GeneralSettingsViewModel(GeneralSettings model)
        : this(model, null) { }

    public GeneralSettingsViewModel(GeneralSettings model, IDialogService? dialog)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _autoUpdateCheck = model.AutoUpdateCheck;
        _backupDirectory = model.BackupDirectory ?? string.Empty;
        _confirmOnReset = model.ConfirmOnReset;
        _confirmOnExit = model.ConfirmOnExit;
        _dialog = dialog;

        BrowseBackupDirectoryCommand = new RelayCommand(ExecuteBrowse, () => _dialog is not null);
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

    public RelayCommand BrowseBackupDirectoryCommand { get; }

    public GeneralSettings ToModel() => new()
    {
        AutoUpdateCheck = _autoUpdateCheck,
        BackupDirectory = _backupDirectory,
        ConfirmOnReset = _confirmOnReset,
        ConfirmOnExit = _confirmOnExit,
    };

    private void ExecuteBrowse()
    {
        if (_dialog is null) return;
        var picked = _dialog.ShowFolderBrowserDialog("バックアップ保存先の選択", _backupDirectory);
        if (!string.IsNullOrEmpty(picked))
            BackupDirectory = picked;
    }
}
