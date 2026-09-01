using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Updates;

namespace InfTimestamper.Services;

public interface IDialogService
{
    IReadOnlyList<DateTimeOffset>? ShowDateTimeEditor(IReadOnlyList<DateTimeOffset> currentValues);

    AppSettings? ShowSettings(AppSettings current);

    string? ShowOpenFileDialog(string filter, string? initialDirectory = null);

    string? ShowSaveFileDialog(string filter, string defaultFileName, string? initialDirectory = null);

    string? ShowFolderBrowserDialog(string title, string? initialDirectory = null);

    void ShowError(string title, string message);

    void ShowInfo(string title, string message);

    bool Confirm(string title, string message);

    /// <summary>異常終了復旧の 3 択（読み込む / 無視する / 削除する）を尋ねる。</summary>
    UnfinishedRecordChoice ConfirmUnfinishedRecord(UnfinishedRecord record);

    Task<bool> ShowUpdateProgressAsync(IUpdateService updateService, CancellationToken cancellationToken);
}
