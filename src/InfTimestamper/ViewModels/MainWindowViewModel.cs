using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Persistence.Json;
using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUlid;

namespace InfTimestamper.ViewModels;

public sealed class MainWindowViewModel : ObservableBase
{
    public const string DefaultFormat = "$timestamp $title [$diff_s $level]";
    public const string JsonFileFilter = "JSON ファイル (*.json)|*.json|すべてのファイル (*.*)|*.*";
    public const string GitHubUrl = "https://github.com/Freedom645/inf-timestamper";

    private readonly AppStateMachine _stateMachine;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialog;
    private readonly JsonRecordStore _recordStore;
    private readonly SettingsStore? _settingsStore;
    private readonly string? _settingsPath;
    private readonly ILogger<MainWindowViewModel> _logger;

    private AppSettings _settings = AppSettings.CreateDefault();
    private StreamRecord _record = new();
    private string _format = DefaultFormat;
    private string _hintText = string.Empty;
    private ObsConnectionManagerState _obsStatus = ObsConnectionManagerState.Idle;
    private int _obsRetryAttempt;
    private RecordingCoordinator? _coordinator;

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        ILogger<MainWindowViewModel>? logger = null)
        : this(stateMachine, clipboard, dialog, recordStore, AppSettings.CreateDefault(), null, null, logger) { }

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        AppSettings settings,
        SettingsStore? settingsStore,
        string? settingsPath,
        ILogger<MainWindowViewModel>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _settings = settings ?? AppSettings.CreateDefault();
        _settingsStore = settingsStore;
        _settingsPath = settingsPath;
        _format = string.IsNullOrEmpty(_settings.Infinitas?.TimestampFormat)
            ? DefaultFormat
            : _settings.Infinitas!.TimestampFormat;
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;

        _stateMachine.StateChanged += OnStateMachineChanged;
        Timestamps.CollectionChanged += OnTimestampsChanged;

        StartCommand = new RelayCommand(ExecuteStart, () => State == AppState.Initial);
        ForceStartCommand = new RelayCommand(ExecuteForceStart, () => State == AppState.WaitingForStream);
        StopCommand = new RelayCommand(ExecuteStop,
            () => State is AppState.WaitingForStream or AppState.Recording);
        ResumeCommand = new RelayCommand(ExecuteResume, () => State == AppState.RecordingEnded);
        ResetCommand = new RelayCommand(ExecuteReset, () => State == AppState.RecordingEnded);
        CopyCommand = new RelayCommand(ExecuteCopy, () => Timestamps.Count > 0);

        EditStreamStartedAtCommand = new RelayCommand(ExecuteEditStreamStartedAt, () => StreamStartedAt is not null);
        EditSelectedTimestampsCommand = new RelayCommand(ExecuteEditSelectedTimestamps,
            () => Timestamps.Any(t => t.IsSelected));
        OpenRecordCommand = new RelayCommand(ExecuteOpenRecord, () => State == AppState.Initial);
        SaveRecordCommand = new RelayCommand(ExecuteSaveRecord,
            () => Timestamps.Count > 0 || StreamStartedAt is not null);
        OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);

        ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
        OpenGitHubCommand = new RelayCommand(ExecuteOpenGitHub);
    }

    public ObservableCollection<TimestampViewModel> Timestamps { get; } = new();

    public AppState State => _stateMachine.State;

    public string GameName => "INFINITAS";

    public string StateLabel
    {
        get
        {
            // OBS 再接続中は要件 L535 に従ったラベルを優先表示
            if (_obsStatus == ObsConnectionManagerState.Reconnecting && _obsRetryAttempt >= 1)
                return $"OBS再接続中（試行 {_obsRetryAttempt} 回目）";

            return State switch
            {
                AppState.Initial => "初期状態",
                AppState.WaitingForStream => "配信開始待ち",
                AppState.Recording => "記録中",
                AppState.RecordingEnded => "記録終了",
                _ => "-",
            };
        }
    }

    public string PrimaryButtonText => State switch
    {
        AppState.Initial => "開始",
        AppState.WaitingForStream => "強制開始",
        AppState.Recording => "記録停止",
        AppState.RecordingEnded => "記録再開",
        _ => "-",
    };

    public RelayCommand PrimaryCommand => State switch
    {
        AppState.Initial => StartCommand,
        AppState.WaitingForStream => ForceStartCommand,
        AppState.Recording => StopCommand,
        AppState.RecordingEnded => ResumeCommand,
        _ => StartCommand,
    };

    public string HintText
    {
        get => _hintText;
        set => SetField(ref _hintText, value ?? string.Empty);
    }

    public string Format
    {
        get => _format;
        set
        {
            var next = value ?? string.Empty;
            if (!SetField(ref _format, next)) return;
            foreach (var ts in Timestamps)
                ts.UpdateFormat(next);
        }
    }

    public DateTimeOffset? StreamStartedAt
        => _record.Stream.StartedAt == default ? null : _record.Stream.StartedAt;

    public string StreamStartedAtText => StreamStartedAt is null
        ? "-"
        : StreamStartedAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    public int TimestampCount => Timestamps.Count;

    public bool CanReset => State == AppState.RecordingEnded;

    public RelayCommand StartCommand { get; }
    public RelayCommand ForceStartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand EditStreamStartedAtCommand { get; }
    public RelayCommand EditSelectedTimestampsCommand { get; }
    public RelayCommand OpenRecordCommand { get; }
    public RelayCommand SaveRecordCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ShowAboutCommand { get; }
    public RelayCommand OpenGitHubCommand { get; }

    public AppSettings CurrentSettings => _settings;

    public void AddTimestamp(TimestampEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        var vm = new TimestampViewModel(entry, _record.Stream.StartedAt, _format);
        InsertSorted(vm);
        _record.Timestamps.Add(entry);
        _record.UpdatedAt = DateTimeOffset.Now;
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
    }

    public void SetStreamStartedAt(DateTimeOffset startedAt)
    {
        _record.Stream.StartedAt = startedAt;
        RaisePropertyChanged(nameof(StreamStartedAt));
        RaisePropertyChanged(nameof(StreamStartedAtText));
        foreach (var ts in Timestamps)
            ts.UpdateStreamStartedAt(startedAt);
        EditStreamStartedAtCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
    }

    public void NotifySelectionChanged()
        => EditSelectedTimestampsCommand.RaiseCanExecuteChanged();

    public void BindCoordinator(RecordingCoordinator coordinator)
    {
        if (coordinator is null) throw new ArgumentNullException(nameof(coordinator));
        if (_coordinator is not null) UnbindCoordinator();

        _coordinator = coordinator;
        _coordinator.PlayStarted += OnPlayStarted;
        _coordinator.PlayResultDetected += OnPlayResultDetected;
        _coordinator.ObsStatusChanged += OnObsStatusChanged;

        ApplySettingsToCoordinator();
    }

    public void UnbindCoordinator()
    {
        if (_coordinator is null) return;
        _coordinator.PlayStarted -= OnPlayStarted;
        _coordinator.PlayResultDetected -= OnPlayResultDetected;
        _coordinator.ObsStatusChanged -= OnObsStatusChanged;
        _coordinator = null;
    }

    private void ApplySettingsToCoordinator()
    {
        if (_coordinator is null) return;

        var streamObs = new ObsConnectionOptions(_settings.Obs.Host, _settings.Obs.Port, _settings.Obs.Password);
        var captureObs = _settings.Infinitas.TwoPcEnabled && _settings.Infinitas.CaptureObs is not null
            ? new ObsConnectionOptions(_settings.Infinitas.CaptureObs.Host, _settings.Infinitas.CaptureObs.Port, _settings.Infinitas.CaptureObs.Password)
            : null;

        _coordinator.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = streamObs,
            CaptureObs = captureObs,
            TwoPcEnabled = _settings.Infinitas.TwoPcEnabled,
            GameSourceName = _settings.Infinitas.GameSourceName,
        });
    }

    private void OnPlayStarted(object? sender, PlayStartedEventArgs e)
    {
        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = e.CapturedAt,
        };
        foreach (var (key, value) in e.Fields)
        {
            if (!string.IsNullOrEmpty(value))
                entry.SetField(key, value);
        }
        AddTimestamp(entry);
    }

    private void OnPlayResultDetected(object? sender, PlayResultEventArgs e)
    {
        // 直近のプレイ開始エントリに結果フィールドをマージ
        var latestEntry = _record.Timestamps.LastOrDefault();
        if (latestEntry is null) return;

        foreach (var (key, value) in e.Fields)
        {
            if (!string.IsNullOrEmpty(value))
                latestEntry.SetField(key, value);
        }
        _record.UpdatedAt = DateTimeOffset.Now;

        var vm = Timestamps.FirstOrDefault(t => ReferenceEquals(t.Entry, latestEntry));
        vm?.NotifyEntryUpdated();
    }

    private void OnObsStatusChanged(object? sender, RecordingObsStatusChangedEventArgs e)
    {
        _obsStatus = e.State;
        _obsRetryAttempt = e.RetryAttempt;
        RaisePropertyChanged(nameof(StateLabel));
    }

    private void ExecuteShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        _dialog.ShowInfo("バージョン情報",
            $"INF-TIMESTAMPER\nバージョン: {version}\n\nGitHub: {GitHubUrl}");
    }

    private void ExecuteOpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub ページを開けませんでした。");
            _dialog.ShowError("GitHub", "既定ブラウザの起動に失敗しました: " + ex.Message);
        }
    }

    public bool RequestExitConfirmation()
    {
        if (_settings.General?.ConfirmOnExit != true) return true;

        var isRecording = State == AppState.Recording;
        var hasEntries = Timestamps.Count > 0;
        if (!isRecording && !hasEntries) return true;

        var message = isRecording
            ? "記録中の状態でアプリを終了しようとしています。終了しますか？"
            : "未保存の記録があります。終了しますか？";
        return _dialog.Confirm("終了確認", message);
    }

    public void CheckUnfinishedRecords()
    {
        var dir = _settings.General?.BackupDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return;

        UnfinishedRecord? unfinished = null;
        try
        {
            unfinished = _recordStore.FindUnfinished(dir).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "未完了ファイルのスキャンに失敗しました。");
            return;
        }
        if (unfinished is null) return;

        var shouldLoad = _dialog.Confirm(
            "前回の記録が完了していません",
            $"前回終了時に未完了の記録が見つかりました。\n\n{unfinished.FilePath}\n\n読み込みますか？\n（「いいえ」を選んだ場合、次回起動時にも検出されます）");

        if (!shouldLoad) return;

        try
        {
            LoadRecord(unfinished.Record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "未完了ファイルの読込に失敗しました。");
            _dialog.ShowError("読込エラー", ex.Message);
        }
    }

    internal StreamRecord Record => _record;

    private void ExecuteStart()
    {
        TryStateOp(() => _stateMachine.Start(), "開始");
    }

    private void ExecuteForceStart()
    {
        TryStateOp(() =>
        {
            _stateMachine.ForceStart();
            SetStreamStartedAt(DateTimeOffset.Now);
        }, "強制開始");
    }

    private void ExecuteStop()
    {
        TryStateOp(() =>
        {
            _stateMachine.Stop();
            if (State == AppState.RecordingEnded)
                _record.Stream.EndedAt = DateTimeOffset.Now;
        }, "停止");
    }

    private void ExecuteResume()
    {
        TryStateOp(() =>
        {
            _stateMachine.Resume();
            _record.Stream.EndedAt = null;
        }, "再開");
    }

    private void ExecuteReset()
    {
        if (_settings.General?.ConfirmOnReset == true)
        {
            if (!_dialog.Confirm("リセット確認", "現在の記録をリセットします。よろしいですか？"))
                return;
        }

        TryStateOp(() =>
        {
            _stateMachine.Reset();
            _record = new StreamRecord();
            Timestamps.Clear();
            RaisePropertyChanged(nameof(StreamStartedAt));
            RaisePropertyChanged(nameof(StreamStartedAtText));
            RaisePropertyChanged(nameof(TimestampCount));
            CopyCommand.RaiseCanExecuteChanged();
            EditStreamStartedAtCommand.RaiseCanExecuteChanged();
            SaveRecordCommand.RaiseCanExecuteChanged();
        }, "リセット");
    }

    private void ExecuteEditStreamStartedAt()
    {
        if (StreamStartedAt is null) return;
        var result = _dialog.ShowDateTimeEditor(new[] { StreamStartedAt.Value });
        if (result is null || result.Count == 0) return;
        SetStreamStartedAt(result[0]);
    }

    private void ExecuteEditSelectedTimestamps()
    {
        var selected = Timestamps.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        var currentValues = selected.Select(t => t.PlayStartedAt).ToList();
        var result = _dialog.ShowDateTimeEditor(currentValues);
        if (result is null || result.Count != selected.Count) return;

        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].Entry.PlayStartedAt = result[i];
            selected[i].NotifyEntryUpdated();
        }
        _record.UpdatedAt = DateTimeOffset.Now;

        ReorderTimestamps();
    }

    private void ExecuteOpenRecord()
    {
        var path = _dialog.ShowOpenFileDialog(JsonFileFilter);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var record = _recordStore.Load(path);
            LoadRecord(record);
        }
        catch (IncompatibleSchemaException ex)
        {
            _logger.LogWarning(ex, "より新しいバージョンのファイルです。");
            _dialog.ShowError("読込エラー", "より新しいバージョンで作成されたファイルです。アプリを最新版に更新してください。");
        }
        catch (UnknownGameException ex)
        {
            _logger.LogWarning(ex, "対応外のゲーム識別子です。");
            _dialog.ShowError("読込エラー", "対応していないゲームのファイルです。");
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "ファイル破損を検出しました。");
            _dialog.ShowError("読込エラー", "ファイルが破損しています。");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "過去記録読込に失敗しました。");
            _dialog.ShowError("読込エラー", ex.Message);
        }
    }

    private void ExecuteOpenSettings()
    {
        var updated = _dialog.ShowSettings(_settings);
        if (updated is null) return;

        _settings = updated;

        // フォーマット文字列の即時反映
        var newFormat = string.IsNullOrEmpty(updated.Infinitas?.TimestampFormat)
            ? DefaultFormat
            : updated.Infinitas!.TimestampFormat;
        Format = newFormat;

        // OBS 接続情報やソース名が変わった可能性があるので Coordinator にも反映
        ApplySettingsToCoordinator();

        // 永続化
        if (_settingsStore is not null && !string.IsNullOrEmpty(_settingsPath))
        {
            try
            {
                _settingsStore.SaveAtomic(_settings, _settingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定の保存に失敗しました。");
                _dialog.ShowError("保存エラー", "設定の保存に失敗しました: " + ex.Message);
            }
        }
    }

    private void ExecuteSaveRecord()
    {
        var fallbackStart = _record.Stream.StartedAt == default ? DateTimeOffset.Now : _record.Stream.StartedAt;
        var defaultName = JsonRecordStore.GenerateFileName(_record.Game, fallbackStart);
        var path = _dialog.ShowSaveFileDialog(JsonFileFilter, defaultName);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            _recordStore.SaveAtomic(_record, path);
            _dialog.ShowInfo("保存完了", $"記録を保存しました:\n{path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存に失敗しました。");
            _dialog.ShowError("保存エラー", ex.Message);
        }
    }

    private void LoadRecord(StreamRecord record)
    {
        if (_stateMachine.State == AppState.Initial)
            _stateMachine.OpenFile();

        _record = record;
        Timestamps.Clear();
        foreach (var entry in record.Timestamps.OrderBy(e => e.PlayStartedAt))
            Timestamps.Add(new TimestampViewModel(entry, record.Stream.StartedAt, _format));

        RaisePropertyChanged(nameof(StreamStartedAt));
        RaisePropertyChanged(nameof(StreamStartedAtText));
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
        EditStreamStartedAtCommand.RaiseCanExecuteChanged();
    }

    private void ReorderTimestamps()
    {
        var sorted = Timestamps.OrderBy(t => t.PlayStartedAt).ToList();
        if (sorted.SequenceEqual(Timestamps)) return;

        Timestamps.Clear();
        foreach (var t in sorted) Timestamps.Add(t);
    }

    private void ExecuteCopy()
    {
        if (Timestamps.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var ts in Timestamps)
            sb.AppendLine(ts.DisplayText);
        try
        {
            _clipboard.SetText(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "クリップボードコピーに失敗しました。");
        }
    }

    private void OnStateMachineChanged(object? sender, StateChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(State));
        RaisePropertyChanged(nameof(StateLabel));
        RaisePropertyChanged(nameof(PrimaryButtonText));
        RaisePropertyChanged(nameof(PrimaryCommand));
        RaisePropertyChanged(nameof(CanReset));

        StartCommand.RaiseCanExecuteChanged();
        ForceStartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
        OpenRecordCommand.RaiseCanExecuteChanged();
    }

    private void OnTimestampsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
        EditSelectedTimestampsCommand.RaiseCanExecuteChanged();
    }

    private void TryStateOp(Action op, string opName)
    {
        try { op(); }
        catch (InvalidStateTransitionException ex)
        {
            _logger.LogWarning(ex, "{Op} 操作が現在の状態 {State} では実行できません。", opName, State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Op} 操作中に予期しないエラーが発生しました。", opName);
        }
    }

    private void InsertSorted(TimestampViewModel vm)
    {
        // 常に PlayStartedAt の昇順を維持
        var index = 0;
        for (; index < Timestamps.Count; index++)
        {
            if (Timestamps[index].PlayStartedAt > vm.PlayStartedAt) break;
        }
        Timestamps.Insert(index, vm);
    }
}
