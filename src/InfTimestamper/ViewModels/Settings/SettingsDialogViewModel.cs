using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class SettingsDialogViewModel : ObservableBase
{
    public SettingsDialogViewModel(AppSettings settings)
        : this(settings, null, null) { }

    public SettingsDialogViewModel(AppSettings settings, IObsConnectionTester? tester, IDialogService? dialog)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        General = new GeneralSettingsViewModel(
            settings.General ?? new GeneralSettings { BackupDirectory = AppSettings.DefaultBackupDirectory() },
            dialog);

        Obs = new ObsSettingsViewModel(
            settings.Obs ?? new ObsConnectionSettings(),
            tester,
            dialog);

        Infinitas = new InfinitasSettingsViewModel(
            settings.Infinitas ?? new InfinitasSettings { TimestampFormat = AppSettings.DefaultTimestampFormat },
            tester,
            dialog,
            ResolveActiveCaptureObs);

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

    private ObsConnectionOptions ResolveActiveCaptureObs()
        => Infinitas.TwoPcEnabled
            ? Infinitas.CaptureObs.ToOptions()
            : Obs.ToOptions();

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
