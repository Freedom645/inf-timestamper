using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Updates;
using InfTimestamper.Services;

namespace InfTimestamper.Core.Tests.ViewModels;

internal sealed class FakeDialogService : IDialogService
{
    public IReadOnlyList<DateTimeOffset>? DateTimeEditorResult { get; set; }
    public AppSettings? SettingsResult { get; set; }
    public string? OpenFileResult { get; set; }
    public string? SaveFileResult { get; set; }
    public string? FolderBrowserResult { get; set; }
    public bool ConfirmResult { get; set; } = true;

    /// <summary>異常終了復旧ダイアログの応答。提示順に消費し、尽きたら最後の値を返し続ける。</summary>
    public List<UnfinishedRecordChoice> UnfinishedChoices { get; } = new();

    public List<(string Title, string Message)> Errors { get; } = new();
    public List<(string Title, string Message)> Infos { get; } = new();
    public IReadOnlyList<DateTimeOffset>? LastDateTimeEditorInput { get; private set; }
    public AppSettings? LastSettingsInput { get; private set; }

    public IReadOnlyList<DateTimeOffset>? ShowDateTimeEditor(IReadOnlyList<DateTimeOffset> currentValues)
    {
        LastDateTimeEditorInput = currentValues;
        return DateTimeEditorResult;
    }

    public AppSettings? ShowSettings(AppSettings current)
    {
        LastSettingsInput = current;
        return SettingsResult;
    }

    public string? ShowOpenFileDialog(string filter, string? initialDirectory = null) => OpenFileResult;

    public string? ShowSaveFileDialog(string filter, string defaultFileName, string? initialDirectory = null)
        => SaveFileResult;

    public string? ShowFolderBrowserDialog(string title, string? initialDirectory = null)
        => FolderBrowserResult;

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public void ShowInfo(string title, string message) => Infos.Add((title, message));

    public bool Confirm(string title, string message) => ConfirmResult;

    public List<UnfinishedRecord> UnfinishedPrompts { get; } = new();

    public UnfinishedRecordChoice ConfirmUnfinishedRecord(UnfinishedRecord record)
    {
        UnfinishedPrompts.Add(record);

        if (UnfinishedChoices.Count == 0)
            return ConfirmResult ? UnfinishedRecordChoice.Load : UnfinishedRecordChoice.Ignore;

        var index = Math.Min(UnfinishedPrompts.Count - 1, UnfinishedChoices.Count - 1);
        return UnfinishedChoices[index];
    }

    public bool UpdateProgressResult { get; set; } = true;
    public int UpdateProgressCallCount { get; private set; }

    public Task<bool> ShowUpdateProgressAsync(IUpdateService updateService, CancellationToken cancellationToken)
    {
        UpdateProgressCallCount++;
        return Task.FromResult(UpdateProgressResult);
    }
}
