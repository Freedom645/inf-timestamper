using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Settings;
using InfTimestamper.Services;

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

    private readonly IDialogService? _dialog;

    private string _timestampFormat;
    private string _refluxDirectory;
    private string _selectedIdentifier;

    public InfinitasSettingsViewModel(InfinitasSettings model)
        : this(model, null) { }

    public InfinitasSettingsViewModel(InfinitasSettings model, IDialogService? dialog)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _timestampFormat = string.IsNullOrEmpty(model.TimestampFormat)
            ? AppSettings.DefaultTimestampFormat
            : model.TimestampFormat;
        _refluxDirectory = model.RefluxDirectory ?? string.Empty;
        AvailableIdentifiers = FormatExpander.SupportedKeys.ToList();
        _selectedIdentifier = AvailableIdentifiers.Count > 0 ? AvailableIdentifiers[0] : string.Empty;
        _dialog = dialog;

        BrowseRefluxDirectoryCommand = new RelayCommand(ExecuteBrowseRefluxDirectory, () => _dialog is not null);
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

    public string RefluxDirectory
    {
        get => _refluxDirectory;
        set => SetField(ref _refluxDirectory, value ?? string.Empty);
    }

    public IReadOnlyList<string> AvailableIdentifiers { get; }

    public string SelectedIdentifier
    {
        get => _selectedIdentifier;
        set => SetField(ref _selectedIdentifier, value ?? string.Empty);
    }

    public string Preview => FormatExpander.Expand(_timestampFormat, PreviewFields);

    public RelayCommand BrowseRefluxDirectoryCommand { get; }

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
        RefluxDirectory = _refluxDirectory,
    };

    private void ExecuteBrowseRefluxDirectory()
    {
        if (_dialog is null) return;
        var picked = _dialog.ShowFolderBrowserDialog("Reflux 出力ディレクトリの選択", _refluxDirectory);
        if (!string.IsNullOrEmpty(picked))
            RefluxDirectory = picked;
    }
}
