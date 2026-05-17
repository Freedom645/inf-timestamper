using System.Windows;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public SettingsDialog(SettingsDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // PasswordBox は SecureString のため Binding 非対応。VM から流し込み、
        // 確定前に VM に戻す
        ObsPasswordBox.Password = viewModel.Obs.Password;
        CaptureObsPasswordBox.Password = viewModel.Infinitas.CaptureObs.Password;

        viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnRequestClose;

        void OnRequestClose()
        {
            // VM 確定処理の直前に PasswordBox の値を VM に戻す
            viewModel.Obs.Password = ObsPasswordBox.Password;
            viewModel.Infinitas.CaptureObs.Password = CaptureObsPasswordBox.Password;
            DialogResult = viewModel.DialogResult;
            Close();
        }
    }

    private void OnAddIdentifierClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsDialogViewModel vm) return;
        var pos = FormatTextBox.SelectionStart;
        vm.Infinitas.InsertIdentifierAtCursor(pos);
        // 挿入後にキャレットを進める（"$xxx" 分）
        var inserted = "$" + vm.Infinitas.SelectedIdentifier;
        FormatTextBox.Focus();
        FormatTextBox.CaretIndex = Math.Min(pos + inserted.Length, FormatTextBox.Text.Length);
    }
}
