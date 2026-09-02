using InfTimestamper.Core.Models;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// 外部ツールの出力ディレクトリを監視するゲーム（INFINITAS / pop'n music）のタブ用 ViewModel。
/// 「参照... でフォルダを選ぶ」という UI がそのまま共通なのでここに寄せている。
/// </summary>
public abstract class DirectoryWatchSettingsViewModel : GameFormatSettingsViewModel
{
    private readonly IDialogService? _dialog;
    private readonly string _browseDialogTitle;

    private string _watchDirectory;

    protected DirectoryWatchSettingsViewModel(
        GameId game,
        string? timestampFormat,
        string? watchDirectory,
        string browseDialogTitle,
        IDialogService? dialog)
        : base(game, timestampFormat)
    {
        _watchDirectory = watchDirectory ?? string.Empty;
        _browseDialogTitle = browseDialogTitle;
        _dialog = dialog;

        BrowseWatchDirectoryCommand = new RelayCommand(ExecuteBrowseWatchDirectory, () => _dialog is not null);
    }

    /// <summary>ゲーム検知に使う外部ツールの出力ディレクトリ。</summary>
    public string WatchDirectory
    {
        get => _watchDirectory;
        set
        {
            if (SetField(ref _watchDirectory, value ?? string.Empty))
                RaisePropertyChanged(nameof(WatchTarget));
        }
    }

    public override string WatchTarget => _watchDirectory;

    public RelayCommand BrowseWatchDirectoryCommand { get; }

    private void ExecuteBrowseWatchDirectory()
    {
        if (_dialog is null) return;
        var picked = _dialog.ShowFolderBrowserDialog(_browseDialogTitle, _watchDirectory);
        if (!string.IsNullOrEmpty(picked))
            WatchDirectory = picked;
    }
}
