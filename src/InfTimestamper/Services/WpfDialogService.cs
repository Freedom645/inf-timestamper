using System.Windows;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Updates;
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

    public async Task<bool> ShowUpdateProgressAsync(IUpdateService updateService, CancellationToken cancellationToken)
    {
        if (updateService is null) throw new ArgumentNullException(nameof(updateService));

        var vm = new UpdateProgressViewModel
        {
            StatusText = "アップデートをダウンロードしています...",
        };
        var window = new UpdateProgressWindow(vm) { Owner = _ownerProvider() };

        bool success = false;
        Exception? failure = null;

        var progress = new Progress<int>(p =>
        {
            vm.ProgressPercent = p;
            vm.StatusText = $"ダウンロード中... {p}%";
        });

        // Window 表示前に非同期処理を開始するため Task.Run + Dispatcher
        async Task RunDownloadAsync()
        {
            try
            {
                success = await updateService.CheckAndDownloadAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
                success = false;
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(window.Close);
            }
        }

        // ShowDialog はブロッキングなのでダウンロードを先に Fire
        var task = RunDownloadAsync();
        window.ShowDialog();
        await task.ConfigureAwait(true);

        return success && failure is null;
    }
}
