using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace InfTimestamper.Behaviors;

/// <summary>
/// タイムスタンプフォーマットの入力欄で、<c>$</c> の直後にキャレットがあるときに識別子候補を出す
/// （要件「"$"の右隣にカーソルがある場合にサジェストされる」）。
///
/// 候補は <see cref="Popup"/> に出すだけでフォーカスは移さず、キー操作は TextBox 側の
/// <c>PreviewKeyDown</c> で捌く。こうしないと入力の流れが途切れる。
/// </summary>
public sealed class IdentifierSuggestion
{
    /// <summary>`$` に続く識別子として成立する文字（<see cref="Core.Formatting.FormatExpander"/> と同じ字種）。</summary>
    private static readonly Regex TokenPattern = new(@"\$([a-z0-9_]*)$", RegexOptions.Compiled);

    private readonly TextBox _textBox;
    private readonly IReadOnlyList<string> _identifiers;
    private readonly Popup _popup;
    private readonly ListBox _list;

    /// <summary>候補確定時に置換する対象範囲（`$` の位置とその時点のキャレット）。</summary>
    private int _tokenStart = -1;

    public IdentifierSuggestion(TextBox textBox, IReadOnlyList<string> identifiers)
    {
        _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
        _identifiers = identifiers ?? Array.Empty<string>();

        _list = new ListBox
        {
            MaxHeight = 160,
            MinWidth = 160,
            FontFamily = new FontFamily("Consolas, Cascadia Mono"),
            // フォーカスを奪うと TextBox の LostFocus で候補が閉じてしまうので、
            // 候補一覧はフォーカスを取らずマウス押下だけを拾う
            Focusable = false,
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

    private void Refresh()
    {
        var caret = _textBox.CaretIndex;
        var text = _textBox.Text ?? string.Empty;
        if (caret < 0 || caret > text.Length)
        {
            Hide();
            return;
        }

        var match = TokenPattern.Match(text[..caret]);
        if (!match.Success)
        {
            Hide();
            return;
        }

        var prefix = match.Groups[1].Value;
        var candidates = _identifiers
            .Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            Hide();
            return;
        }

        _tokenStart = match.Index;
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
        if (tokenStart < 0 || _list.SelectedItem is not string identifier)
        {
            Hide();
            return;
        }

        var text = _textBox.Text ?? string.Empty;
        var caret = Math.Min(_textBox.CaretIndex, text.Length);
        if (tokenStart > caret)
        {
            Hide();
            return;
        }

        var replacement = "$" + identifier;
        _textBox.Text = text[..tokenStart] + replacement + text[caret..];
        _textBox.CaretIndex = tokenStart + replacement.Length;
        _textBox.Focus();
        Hide();
    }
}
