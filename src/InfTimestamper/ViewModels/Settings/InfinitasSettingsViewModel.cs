using System.Collections.ObjectModel;
using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Obs;
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

    private readonly IObsConnectionTester? _tester;
    private readonly IDialogService? _dialog;
    private readonly Func<ObsConnectionOptions>? _resolveCaptureObs;

    private string _timestampFormat;
    private string _gameSourceName;
    private bool _twoPcEnabled;
    private string _selectedIdentifier;
    private bool _isFetchingSources;

    public InfinitasSettingsViewModel(InfinitasSettings model)
        : this(model, null, null, null) { }

    public InfinitasSettingsViewModel(
        InfinitasSettings model,
        IObsConnectionTester? tester,
        IDialogService? dialog,
        Func<ObsConnectionOptions>? resolveCaptureObs)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        _timestampFormat = string.IsNullOrEmpty(model.TimestampFormat)
            ? AppSettings.DefaultTimestampFormat
            : model.TimestampFormat;
        _gameSourceName = model.GameSourceName ?? string.Empty;
        _twoPcEnabled = model.TwoPcEnabled;
        CaptureObs = new ObsSettingsViewModel(model.CaptureObs ?? new ObsConnectionSettings(), tester, dialog);
        AvailableIdentifiers = FormatExpander.SupportedKeys.ToList();
        _selectedIdentifier = AvailableIdentifiers.Count > 0 ? AvailableIdentifiers[0] : string.Empty;
        _tester = tester;
        _dialog = dialog;
        _resolveCaptureObs = resolveCaptureObs;

        AvailableSources = new ObservableCollection<string>();
        FetchSourcesCommand = new RelayCommand(
            ExecuteFetchSources,
            () => _tester is not null && _dialog is not null && _resolveCaptureObs is not null && !_isFetchingSources);
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

    public ObservableCollection<string> AvailableSources { get; }

    public string SelectedIdentifier
    {
        get => _selectedIdentifier;
        set => SetField(ref _selectedIdentifier, value ?? string.Empty);
    }

    public bool IsFetchingSources
    {
        get => _isFetchingSources;
        private set
        {
            if (SetField(ref _isFetchingSources, value))
                FetchSourcesCommand.RaiseCanExecuteChanged();
        }
    }

    public string Preview => FormatExpander.Expand(_timestampFormat, PreviewFields);

    public RelayCommand FetchSourcesCommand { get; }

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

    private async void ExecuteFetchSources()
    {
        if (_tester is null || _dialog is null || _resolveCaptureObs is null) return;
        IsFetchingSources = true;
        try
        {
            var options = _resolveCaptureObs();
            var sources = await _tester.FetchSourceNamesAsync(options, CancellationToken.None).ConfigureAwait(true);

            AvailableSources.Clear();
            foreach (var s in sources)
                AvailableSources.Add(s);

            if (sources.Count == 0)
                _dialog.ShowInfo("OBS から取得", "ソースが見つかりませんでした。");
        }
        catch (Exception ex)
        {
            _dialog.ShowError("OBS から取得", "取得に失敗しました: " + ex.Message);
        }
        finally
        {
            IsFetchingSources = false;
        }
    }
}
