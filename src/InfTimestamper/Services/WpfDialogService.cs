using System.Windows;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.ViewModels;
using InfTimestamper.ViewModels.Settings;
using InfTimestamper.Views;
using Microsoft.Win32;

namespace InfTimestamper.Services;

public sealed class WpfDialogService : IDialogService
{
    private readonly Func<Window?> _ownerProvider;
    private readonly IObsConnectionTester? _tester;

    public WpfDialogService(Func<Window?> ownerProvider)
        : this(ownerProvider, null) { }

    public WpfDialogService(Func<Window?> ownerProvider, IObsConnectionTester? tester)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _tester = tester;
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
        var vm = new SettingsDialogViewModel(current, _tester, this);
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

    public string? ShowFolderBrowserDialog(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
        };
        if (!string.IsNullOrEmpty(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog(_ownerProvider()) == true ? dialog.FolderName : null;
    }

    public void ShowError(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string title, string message)
        => MessageBox.Show(_ownerProvider(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
           == MessageBoxResult.Yes;
}
