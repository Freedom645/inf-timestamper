using InfTimestamper.Core.Settings;

namespace InfTimestamper.Services;

public interface IDialogService
{
    IReadOnlyList<DateTimeOffset>? ShowDateTimeEditor(IReadOnlyList<DateTimeOffset> currentValues);

    AppSettings? ShowSettings(AppSettings current);

    string? ShowOpenFileDialog(string filter, string? initialDirectory = null);

    string? ShowSaveFileDialog(string filter, string defaultFileName, string? initialDirectory = null);

    void ShowError(string title, string message);

    void ShowInfo(string title, string message);

    bool Confirm(string title, string message);
}
