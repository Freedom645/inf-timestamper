using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Services;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// ゲーム別タブ（INFINITAS / pop'n music）に共通する設定項目の ViewModel。
/// 「タイムスタンプフォーマット + 識別子の挿入 + プレビュー + 検知ツールの出力ディレクトリ」という
/// 構成はゲーム間で同じなので、ここに集約してゲーム固有の差分は
/// <see cref="GameCatalog"/> と派生クラスの <c>ToModel</c> に閉じる。
/// </summary>
public abstract class GameFormatSettingsViewModel : ObservableBase
{
    private readonly IDialogService? _dialog;
    private readonly string _browseDialogTitle;

    private string _timestampFormat;
    private string _watchDirectory;
    private string _selectedIdentifier;

    protected GameFormatSettingsViewModel(
        GameId game,
        string? timestampFormat,
        string? watchDirectory,
        string browseDialogTitle,
        IDialogService? dialog)
    {
        Game = game;
        _timestampFormat = string.IsNullOrEmpty(timestampFormat)
            ? Core.Settings.AppSettings.DefaultTimestampFormat
            : timestampFormat;
        _watchDirectory = watchDirectory ?? string.Empty;
        _browseDialogTitle = browseDialogTitle;
        _dialog = dialog;

        AvailableIdentifiers = GameCatalog.Identifiers(game);
        _selectedIdentifier = AvailableIdentifiers.Count > 0 ? AvailableIdentifiers[0] : string.Empty;

        BrowseWatchDirectoryCommand = new RelayCommand(ExecuteBrowseWatchDirectory, () => _dialog is not null);
    }

    public GameId Game { get; }

    public string TimestampFormat
    {
        get => _timestampFormat;
        set
        {
            if (!SetField(ref _timestampFormat, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(Preview));
        }
    }

    /// <summary>ゲーム検知に使う外部ツールの出力ディレクトリ。</summary>
    public string WatchDirectory
    {
        get => _watchDirectory;
        set => SetField(ref _watchDirectory, value ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableIdentifiers { get; }

    public string SelectedIdentifier
    {
        get => _selectedIdentifier;
        set => SetField(ref _selectedIdentifier, value ?? string.Empty);
    }

    /// <summary>要件どおり、ハードコードのダミーデータで展開結果を見せる。</summary>
    public string Preview => FormatExpander.Expand(_timestampFormat, GameCatalog.PreviewFields(Game));

    public RelayCommand BrowseWatchDirectoryCommand { get; }

    public void InsertIdentifierAtCursor(int cursorPosition, string? identifier = null)
    {
        var key = identifier ?? _selectedIdentifier;
        if (string.IsNullOrEmpty(key)) return;

        var pos = Math.Clamp(cursorPosition, 0, _timestampFormat?.Length ?? 0);
        TimestampFormat = (_timestampFormat ?? string.Empty).Insert(pos, "$" + key);
    }

    private void ExecuteBrowseWatchDirectory()
    {
        if (_dialog is null) return;
        var picked = _dialog.ShowFolderBrowserDialog(_browseDialogTitle, _watchDirectory);
        if (!string.IsNullOrEmpty(picked))
            WatchDirectory = picked;
    }
}
