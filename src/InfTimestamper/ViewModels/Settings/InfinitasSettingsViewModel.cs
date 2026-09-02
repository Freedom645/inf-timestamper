using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

public sealed class InfinitasSettingsViewModel : DirectoryWatchSettingsViewModel
{
    public const string BrowseDialogTitle = "Reflux 出力ディレクトリの選択";

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
    }

    public InfinitasSettings ToModel() => new()
    {
        TimestampFormat = TimestampFormat,
        RefluxDirectory = WatchDirectory,
    };
}
