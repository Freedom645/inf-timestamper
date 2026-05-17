using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using InfTimestamper.ViewModels;

namespace InfTimestamper;

public partial class MainWindow : Window
{
    public MainWindow() : this(null) { }

    public MainWindow(MainWindowViewModel? viewModel)
    {
        InitializeComponent();

        if (viewModel is not null)
            DataContext = viewModel;

        if (DataContext is MainWindowViewModel vm)
        {
            HookCollectionAutoScroll(vm);
            Loaded += (_, _) => vm.CheckUnfinishedRecords();
            Closing += OnWindowClosing;
        }
    }

    private void HookCollectionAutoScroll(MainWindowViewModel vm)
    {
        ((INotifyCollectionChanged)vm.Timestamps).CollectionChanged += OnTimestampsChanged;
    }

    private void OnTimestampsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (TimestampList.Items.Count == 0) return;
        TimestampList.ScrollIntoView(TimestampList.Items[TimestampList.Items.Count - 1]);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        e.Cancel = !vm.RequestExitConfirmation();
    }
}
