using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Behaviors;

/// <summary>
/// タイムスタンプフォーマットの入力欄で、<c>$</c> の直後にキャレットがあるときに識別子候補を出す
/// （要件「"$"の右隣にカーソルがある場合にサジェストされる」）。
///
/// 候補は <see cref="Popup"/> に出すだけでフォーカスは移さず、キー操作は TextBox 側の
/// <c>PreviewKeyDown</c> で捌く。こうしないと入力の流れが途切れる。
/// トークンの切り出し・絞り込み・確定時の置換は <see cref="IdentifierCompletion"/> に置いてある。
/// </summary>
public sealed class IdentifierSuggestion
{
    private readonly TextBox _textBox;
    private readonly IReadOnlyList<IdentifierChoice> _identifiers;
    private readonly Popup _popup;
    private readonly ListBox _list;

    /// <summary>候補確定時に置換する対象範囲の開始位置（`$` の位置）。</summary>
    private int _tokenStart = -1;

    public IdentifierSuggestion(TextBox textBox, IReadOnlyList<IdentifierChoice> identifiers)
    {
        _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
        _identifiers = identifiers ?? Array.Empty<IdentifierChoice>();

        _list = new ListBox
        {
            MaxHeight = 160,
            MinWidth = 220,
            // フォーカスを奪うと TextBox の LostFocus で候補が閉じてしまうので、
            // 候補一覧はフォーカスを取らずマウス押下だけを拾う
            Focusable = false,
            ItemTemplate = BuildItemTemplate(),
        };
        _list.PreviewMouseLeftButtonDown += OnListMouseDown;

        _popup = new Popup
        {
            PlacementTarget = _textBox,
            Placement = PlacementMode.Bottom,
            StaysOpen = true,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = SystemColors.WindowBrush,
                BorderBrush = SystemColors.ActiveBorderBrush,
                BorderThickness = new Thickness(1),
                Child = _list,
            },
        };

        _textBox.TextChanged += (_, _) => Refresh();
        _textBox.SelectionChanged += (_, _) => Refresh();
        _textBox.LostFocus += (_, _) => Hide();
        _textBox.PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>候補 1 行の見た目。<c>$key</c> を等幅で、論理名をグレーで添える。</summary>
    private static DataTemplate BuildItemTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var token = new FrameworkElementFactory(typeof(TextBlock));
        token.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(IdentifierChoice.Token)));
        token.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Cascadia Mono"));
        panel.AppendChild(token);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(IdentifierChoice.Label)));
        label.SetValue(TextBlock.ForegroundProperty, SystemColors.GrayTextBrush);
        label.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        panel.AppendChild(label);

        return new DataTemplate { VisualTree = panel };
    }

    private void Refresh()
    {
        if (!IdentifierCompletion.TryGetToken(_textBox.Text, _textBox.CaretIndex, out var tokenStart, out var prefix))
        {
            Hide();
            return;
        }

        var candidates = IdentifierCompletion.Filter(_identifiers, prefix);
        if (candidates.Count == 0)
        {
            Hide();
            return;
        }

        _tokenStart = tokenStart;
        _list.ItemsSource = candidates;
        _list.SelectedIndex = 0;
        _popup.IsOpen = true;
    }

    private void Hide()
    {
        _popup.IsOpen = false;
        _tokenStart = -1;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_popup.IsOpen) return;

        switch (e.Key)
        {
            case Key.Down:
                Move(1);
                e.Handled = true;
                break;
            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void Move(int delta)
    {
        if (_list.Items.Count == 0) return;
        var next = _list.SelectedIndex + delta;
        _list.SelectedIndex = Math.Clamp(next, 0, _list.Items.Count - 1);
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(_list, (DependencyObject)e.OriginalSource) is not ListBoxItem container)
            return;

        _list.SelectedItem = container.DataContext;
        Commit();
        e.Handled = true;
    }

    private void Commit()
    {
        // Text を書き換えると TextChanged 経由で Refresh が走り _tokenStart が変わるので、先に控える
        var tokenStart = _tokenStart;
        if (tokenStart < 0 || _list.SelectedItem is not IdentifierChoice choice)
        {
            Hide();
            return;
        }

        var (text, caret) = IdentifierCompletion.Apply(
            _textBox.Text, tokenStart, _textBox.CaretIndex, choice.Key);

        _textBox.Text = text;
        _textBox.CaretIndex = caret;
        _textBox.Focus();
        Hide();
    }
}
