using System.Windows;
using System.Windows.Controls;
using InfTimestamper.Behaviors;
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

        // "$" の直後で識別子候補を出す（要件「"$"の右隣にカーソルがある場合にサジェストされる」）
        _ = new IdentifierSuggestion(InfinitasFormatTextBox, viewModel.Infinitas.AvailableIdentifiers);
        _ = new IdentifierSuggestion(PopnFormatTextBox, viewModel.Popn.AvailableIdentifiers);
        _ = new IdentifierSuggestion(SdvxFormatTextBox, viewModel.Sdvx.AvailableIdentifiers);

        // PasswordBox は SecureString のため Binding 非対応。VM から流し込み、
        // 確定前に VM に戻す
        ObsPasswordBox.Password = viewModel.Obs.Password;

        // クライアントシークレットは「ログイン」ボタンで確定前に使うので、入力のたびに VM へ流す
        YouTubeClientSecretBox.Password = viewModel.YouTube.ClientSecret;
        YouTubeClientSecretBox.PasswordChanged += (_, _) =>
            viewModel.YouTube.ClientSecret = YouTubeClientSecretBox.Password;

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

    private void OnAddSdvxIdentifierClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsDialogViewModel vm) return;
        InsertIdentifier(vm.Sdvx, SdvxFormatTextBox);
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
