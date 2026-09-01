using InfTimestamper.Core.Formatting;

namespace InfTimestamper.ViewModels;

/// <summary>
/// タイムスタンプリストの先頭に置く「配信開始」行。
/// YouTube のチャプタ機能は先頭が 0 秒のチャプタであることを要求するため、
/// 相対時刻は常に <see cref="TimeSpan.Zero"/>（= <c>00:00:00</c>）で表示する。
/// プレイ記録ではないのでバックアップ JSON には含めない。
/// </summary>
public sealed class StreamStartRowViewModel : ObservableBase, ITimestampRow
{
    private string _label;

    public StreamStartRowViewModel(string label)
    {
        _label = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
    }

    public string Label
    {
        get => _label;
        set
        {
            if (!SetField(ref _label, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(DisplayText));
        }
    }

    public string DisplayText => $"{FormatExpander.FormatTimestamp(TimeSpan.Zero)} {_label}";
}
