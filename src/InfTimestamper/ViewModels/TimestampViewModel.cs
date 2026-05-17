using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Models;

namespace InfTimestamper.ViewModels;

public sealed class TimestampViewModel : ObservableBase
{
    private readonly TimestampEntry _entry;
    private DateTimeOffset _streamStartedAt;
    private string _format;
    private bool _isSelected;

    public TimestampViewModel(TimestampEntry entry, DateTimeOffset streamStartedAt, string format)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _streamStartedAt = streamStartedAt;
        _format = format ?? string.Empty;
    }

    public TimestampEntry Entry => _entry;

    public DateTimeOffset PlayStartedAt => _entry.PlayStartedAt;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string DisplayText => FormatExpander.Expand(_format, BuildFields());

    public void UpdateFormat(string format)
    {
        if (_format == (format ?? string.Empty)) return;
        _format = format ?? string.Empty;
        RaisePropertyChanged(nameof(DisplayText));
    }

    public void UpdateStreamStartedAt(DateTimeOffset newStart)
    {
        if (_streamStartedAt == newStart) return;
        _streamStartedAt = newStart;
        RaisePropertyChanged(nameof(DisplayText));
    }

    public void NotifyEntryUpdated()
    {
        RaisePropertyChanged(nameof(PlayStartedAt));
        RaisePropertyChanged(nameof(DisplayText));
    }

    private IReadOnlyDictionary<string, string> BuildFields()
    {
        var dict = new Dictionary<string, string>(_entry.Fields.Count + 1);
        foreach (var key in _entry.Fields.Keys)
        {
            if (_entry.TryGetFieldAsString(key, out var value))
                dict[key] = value;
        }

        var relative = _entry.PlayStartedAt - _streamStartedAt;
        dict[FormatExpander.TimestampKey] = FormatExpander.FormatTimestamp(relative);
        return dict;
    }
}
