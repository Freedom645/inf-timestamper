using System.Windows;
using System.Windows.Controls;
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

        viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnRequestClose;

        void OnRequestClose()
        {
            // VM 確定処理の直前に PasswordBox の値を VM に戻す
            viewModel.Obs.Password = ObsPasswordBox.Password;
            DialogResult = viewModel.DialogResult;
            Close();
        }
    }

    private void OnAddInfinitasIdentifierClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsDialogViewModel vm) return;
        InsertIdentifier(vm.Infinitas, InfinitasFormatTextBox);
    }

    private void OnAddPopnIdentifierClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsDialogViewModel vm) return;
        InsertIdentifier(vm.Popn, PopnFormatTextBox);
    }

    private static void InsertIdentifier(GameFormatSettingsViewModel vm, TextBox formatTextBox)
    {
        var pos = formatTextBox.SelectionStart;
        vm.InsertIdentifierAtCursor(pos);
        // 挿入後にキャレットを進める（"$xxx" 分）
        var inserted = "$" + vm.SelectedIdentifier;
        formatTextBox.Focus();
        formatTextBox.CaretIndex = Math.Min(pos + inserted.Length, formatTextBox.Text.Length);
    }
}
