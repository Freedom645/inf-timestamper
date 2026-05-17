using System.Collections.Specialized;
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
            HookCollectionAutoScroll(vm);
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
}
