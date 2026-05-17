using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

namespace InfTimestamper.Core.Tests.ViewModels;

internal sealed class FakeDialogService : IDialogService
{
    public IReadOnlyList<DateTimeOffset>? DateTimeEditorResult { get; set; }
    public AppSettings? SettingsResult { get; set; }
    public string? OpenFileResult { get; set; }
    public string? SaveFileResult { get; set; }
    public bool ConfirmResult { get; set; } = true;

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

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public void ShowInfo(string title, string message) => Infos.Add((title, message));

    public bool Confirm(string title, string message) => ConfirmResult;
}
