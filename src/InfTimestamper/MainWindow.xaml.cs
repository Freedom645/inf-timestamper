using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
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
            Loaded += async (_, _) =>
            {
                vm.CheckUnfinishedRecords();
                if (vm.CurrentSettings.General?.AutoUpdateCheck == true)
                    await vm.CheckLatestVersionAsync(silent: true);
            };
            Closing += OnWindowClosing;
        }
    }

    private void HookCollectionAutoScroll(MainWindowViewModel vm)
    {
        ((INotifyCollectionChanged)vm.DisplayRows).CollectionChanged += OnDisplayRowsChanged;
    }

    private void OnDisplayRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // ListBox 自身がコレクション変更を処理し切る前に ScrollIntoView すると、
        // 項目コンテナの生成と描画がずれて同じ行が二重に見える状態になる。
        // レイアウトが落ち着いたあとに 1 度だけ追尾する。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TimestampList.Items.Count == 0) return;
            TimestampList.ScrollIntoView(TimestampList.Items[TimestampList.Items.Count - 1]);
        }), DispatcherPriority.Background);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (!vm.RequestExitConfirmation())
        {
            e.Cancel = true;
            return;
        }

        vm.SaveOnExit();
    }
}
