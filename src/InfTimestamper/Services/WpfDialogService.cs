using System.Windows;
using InfTimestamper.Core.Settings;
using InfTimestamper.ViewModels;
using InfTimestamper.ViewModels.Settings;
using InfTimestamper.Views;
using Microsoft.Win32;

namespace InfTimestamper.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly Func<Window?> _ownerProvider;

    public WpfDialogService(Func<Window?> ownerProvider)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
    }

    public IReadOnlyList<DateTimeOffset>? ShowDateTimeEditor(IReadOnlyList<DateTimeOffset> currentValues)
    {
        if (currentValues is null || currentValues.Count == 0)
            return null;

        var vm = new DateTimeEditDialogViewModel(currentValues);
        var dialog = new DateTimeEditDialog(vm) { Owner = _ownerProvider() };
        var ok = dialog.ShowDialog();
        return ok == true ? vm.Result : null;
    }

    public AppSettings? ShowSettings(AppSettings current)
    {
        if (current is null) throw new ArgumentNullException(nameof(current));
        var vm = new SettingsDialogViewModel(current);
        var dialog = new SettingsDialog(vm) { Owner = _ownerProvider() };
        var ok = dialog.ShowDialog();
        return ok == true ? vm.Result : null;
    }

    public string? ShowOpenFileDialog(string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog(_ownerProvider()) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string defaultFileName, string? initialDirectory = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            AddExtension = true,
            DefaultExt = ".json",
        };
        if (!string.IsNullOrEmpty(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog(_ownerProvider()) == true ? dialog.FileName : null;
    }

    public void ShowError(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
           == MessageBoxResult.Yes;
}
