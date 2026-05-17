using System.Windows;
using System.Windows.Input;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Behaviors;

public static class HintTextBehavior
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(HintTextBehavior),
            new PropertyMetadata(null, OnTextChanged));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);

    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        element.MouseEnter -= OnMouseEnter;
        element.MouseLeave -= OnMouseLeave;

        if (e.NewValue is string s && !string.IsNullOrEmpty(s))
        {
            element.MouseEnter += OnMouseEnter;
            element.MouseLeave += OnMouseLeave;
        }
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var vm = ResolveMainViewModel(element);
        if (vm is null) return;
        vm.HintText = GetText(element) ?? string.Empty;
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var vm = ResolveMainViewModel(element);
        if (vm is null) return;
        vm.HintText = string.Empty;
    }

    private static MainWindowViewModel? ResolveMainViewModel(FrameworkElement element)
    {
        var window = Window.GetWindow(element);
        return window?.DataContext as MainWindowViewModel;
    }
}
