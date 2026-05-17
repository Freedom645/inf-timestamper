using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Settings;

namespace InfTimestamper.ViewModels.Settings;

public sealed class InfinitasSettingsViewModel : ObservableBase
{
    public static readonly IReadOnlyDictionary<string, string> PreviewFields
        = new Dictionary<string, string>
        {
            ["timestamp"] = "00:01:23",
            ["title"] = "Sample Song",
            ["diff_l"] = "ANOTHER",
            ["diff_s"] = "SPA",
            ["level"] = "11",
            ["miss_count"] = "3",
            ["ex_score"] = "1234",
            ["dj_level"] = "AAA",
            ["lamp"] = "FC",
        };

    private string _timestampFormat;
    private string _gameSourceName;
    private bool _twoPcEnabled;
    private string _selectedIdentifier;

    public InfinitasSettingsViewModel(InfinitasSettings model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _timestampFormat = string.IsNullOrEmpty(model.TimestampFormat)
            ? AppSettings.DefaultTimestampFormat
            : model.TimestampFormat;
        _gameSourceName = model.GameSourceName ?? string.Empty;
        _twoPcEnabled = model.TwoPcEnabled;
        CaptureObs = new ObsSettingsViewModel(model.CaptureObs ?? new ObsConnectionSettings());
        AvailableIdentifiers = FormatExpander.SupportedKeys.ToList();
        _selectedIdentifier = AvailableIdentifiers.Count > 0 ? AvailableIdentifiers[0] : string.Empty;
    }

    public string TimestampFormat
    {
        get => _timestampFormat;
        set
        {
            if (!SetField(ref _timestampFormat, value ?? string.Empty)) return;
            RaisePropertyChanged(nameof(Preview));
        }
    }

    public string GameSourceName
    {
        get => _gameSourceName;
        set => SetField(ref _gameSourceName, value ?? string.Empty);
    }

    public bool TwoPcEnabled
    {
        get => _twoPcEnabled;
        set => SetField(ref _twoPcEnabled, value);
    }

    public ObsSettingsViewModel CaptureObs { get; }

    public IReadOnlyList<string> AvailableIdentifiers { get; }

    public string SelectedIdentifier
    {
        get => _selectedIdentifier;
        set => SetField(ref _selectedIdentifier, value ?? string.Empty);
    }

    public string Preview => FormatExpander.Expand(_timestampFormat, PreviewFields);

    public void InsertIdentifierAtCursor(int cursorPosition, string? identifier = null)
    {
        var key = identifier ?? _selectedIdentifier;
        if (string.IsNullOrEmpty(key)) return;

        var pos = Math.Clamp(cursorPosition, 0, _timestampFormat?.Length ?? 0);
        TimestampFormat = (_timestampFormat ?? string.Empty).Insert(pos, "$" + key);
    }

    public InfinitasSettings ToModel() => new()
    {
        TimestampFormat = _timestampFormat,
        GameSourceName = _gameSourceName,
        TwoPcEnabled = _twoPcEnabled,
        CaptureObs = CaptureObs.ToModel(),
    };
}
