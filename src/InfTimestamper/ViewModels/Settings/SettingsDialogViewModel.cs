using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

public sealed class SettingsDialogViewModel : ObservableBase
{
    public SettingsDialogViewModel(AppSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        General = new GeneralSettingsViewModel(settings.General ?? new GeneralSettings { BackupDirectory = AppSettings.DefaultBackupDirectory() });
        Obs = new ObsSettingsViewModel(settings.Obs ?? new ObsConnectionSettings());
        Infinitas = new InfinitasSettingsViewModel(settings.Infinitas ?? new InfinitasSettings { TimestampFormat = AppSettings.DefaultTimestampFormat });

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public GeneralSettingsViewModel General { get; }
    public ObsSettingsViewModel Obs { get; }
    public InfinitasSettingsViewModel Infinitas { get; }

    public AppSettings? Result { get; private set; }
    public bool? DialogResult { get; private set; }

    public event Action? RequestClose;

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void Confirm()
    {
        Result = new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            General = General.ToModel(),
            Obs = Obs.ToModel(),
            Infinitas = Infinitas.ToModel(),
        };
        DialogResult = true;
        RequestClose?.Invoke();
    }

    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}
