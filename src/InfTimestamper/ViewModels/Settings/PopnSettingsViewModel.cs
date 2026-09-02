using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class PopnSettingsViewModel : DirectoryWatchSettingsViewModel
{
    public const string BrowseDialogTitle = "popn-lively-tracker 出力ディレクトリの選択";

    public PopnSettingsViewModel(PopnSettings model)
        : this(model, null) { }

    public PopnSettingsViewModel(PopnSettings model, IDialogService? dialog)
        : base(
            GameId.Popn,
            (model ?? throw new ArgumentNullException(nameof(model))).TimestampFormat,
            model.TrackerDirectory,
            BrowseDialogTitle,
            dialog)
    {
    }

    public PopnSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        TrackerDirectory = WatchDirectory,
    };
}
