using System.Windows;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Views;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public UpdateProgressWindow(UpdateProgressViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
