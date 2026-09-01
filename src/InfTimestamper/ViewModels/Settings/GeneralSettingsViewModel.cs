using System.IO;
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
    private bool _includeStreamStartRow;
    private string _streamStartRowLabel = AppSettings.DefaultStreamStartRowLabel;
    private string _selectedGame = string.Empty;

    public GeneralSettingsViewModel(GeneralSettings model)
        : this(model, null) { }

    public GeneralSettingsViewModel(GeneralSettings model, IDialogService? dialog)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _autoUpdateCheck = model.AutoUpdateCheck;
        _backupDirectory = model.BackupDirectory ?? string.Empty;
        _confirmOnReset = model.ConfirmOnReset;
        _confirmOnExit = model.ConfirmOnExit;
        _includeStreamStartRow = model.IncludeStreamStartRow;
        _streamStartRowLabel = string.IsNullOrWhiteSpace(model.StreamStartRowLabel)
            ? AppSettings.DefaultStreamStartRowLabel
            : model.StreamStartRowLabel;
        // ゲーム選択はメインウィンドウ側の操作なので、設定ダイアログでは触らずそのまま持ち回る
        _selectedGame = model.SelectedGame ?? string.Empty;
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
        set
        {
            if (!SetField(ref _backupDirectory, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(IsBackupDirectoryValid));
        }
    }

    /// <summary>
    /// 保存先として使えるか（要件「不正なパスや書込権限なしの場合は赤背景表示」）。
    /// 未作成のフォルダも許容するので、存在しない場合は親フォルダに書けるかで判定する。
    /// </summary>
    public bool IsBackupDirectoryValid => ValidateDirectory(_backupDirectory);

    private static bool ValidateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            // パス文字として成立していない
            return false;
        }

        if (Directory.Exists(full)) return CanWriteInto(full);

        var parent = Path.GetDirectoryName(full);
        return !string.IsNullOrEmpty(parent) && Directory.Exists(parent) && CanWriteInto(parent);
    }

    private static bool CanWriteInto(string directory)
    {
        // ACL の解釈は環境差が大きいので、実際に書けるかを小さなファイルで試す
        var probe = Path.Combine(directory, ".inf-timestamper-write-test");
        try
        {
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
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

    /// <summary>タイムスタンプ先頭に「00:00:00 配信開始」行を入れるか（YouTube のチャプタ用）。</summary>
    public bool IncludeStreamStartRow
    {
        get => _includeStreamStartRow;
        set => SetField(ref _includeStreamStartRow, value);
    }

    public string StreamStartRowLabel
    {
        get => _streamStartRowLabel;
        set => SetField(ref _streamStartRowLabel, value ?? string.Empty);
    }

    public RelayCommand BrowseBackupDirectoryCommand { get; }

    public GeneralSettings ToModel() => new()
    {
        AutoUpdateCheck = _autoUpdateCheck,
        BackupDirectory = _backupDirectory,
        ConfirmOnReset = _confirmOnReset,
        ConfirmOnExit = _confirmOnExit,
        IncludeStreamStartRow = _includeStreamStartRow,
        StreamStartRowLabel = string.IsNullOrWhiteSpace(_streamStartRowLabel)
            ? AppSettings.DefaultStreamStartRowLabel
            : _streamStartRowLabel,
        SelectedGame = _selectedGame,
    };

    private void ExecuteBrowse()
    {
        if (_dialog is null) return;
        var picked = _dialog.ShowFolderBrowserDialog("バックアップ保存先の選択", _backupDirectory);
        if (!string.IsNullOrEmpty(picked))
            BackupDirectory = picked;
    }
}
