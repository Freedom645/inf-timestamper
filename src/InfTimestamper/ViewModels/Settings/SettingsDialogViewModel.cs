using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.YouTube;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class SettingsDialogViewModel : ObservableBase
{
    public SettingsDialogViewModel(AppSettings settings)
        : this(settings, null, null) { }

    public SettingsDialogViewModel(
        AppSettings settings,
        IObsConnectionTester? tester,
        IDialogService? dialog,
        YouTubeAccount? youTubeAccount = null,
        IYouTubeApi? youTubeApi = null)
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
            dialog);

        Popn = new PopnSettingsViewModel(
            settings.Popn ?? new PopnSettings { TimestampFormat = AppSettings.DefaultTimestampFormat },
            dialog);

        Sdvx = new SdvxSettingsViewModel(
            settings.Sdvx ?? new SdvxSettings { TimestampFormat = AppSettings.DefaultTimestampFormat },
            dialog);

        YouTube = new YouTubeSettingsViewModel(
            settings.YouTube ?? new YouTubeSettings(),
            youTubeAccount,
            youTubeApi,
            dialog);

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public GeneralSettingsViewModel General { get; }
    public ObsSettingsViewModel Obs { get; }
    public InfinitasSettingsViewModel Infinitas { get; }
    public PopnSettingsViewModel Popn { get; }
    public SdvxSettingsViewModel Sdvx { get; }
    public YouTubeSettingsViewModel YouTube { get; }

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
            Popn = Popn.ToModel(),
            Sdvx = Sdvx.ToModel(),
            YouTube = YouTube.ToModel(),
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
