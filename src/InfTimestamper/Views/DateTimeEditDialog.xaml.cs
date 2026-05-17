using System.Windows;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Views;

public partial class DateTimeEditDialog : Window
{
    public DateTimeEditDialog()
    {
        InitializeComponent();
    }

    public DateTimeEditDialog(DateTimeEditDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnRequestClose;

        void OnRequestClose()
        {
            DialogResult = viewModel.DialogResult;
            Close();
        }
    }
}
