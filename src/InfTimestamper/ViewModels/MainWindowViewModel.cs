using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Persistence.Json;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Updates;
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
    private readonly IGitHubReleaseChecker? _releaseChecker;
    private readonly IUpdateService? _updateService;
    private readonly ILogger<MainWindowViewModel> _logger;

    private AppSettings _settings = AppSettings.CreateDefault();
    private StreamRecord _record = new();
    private string _format = DefaultFormat;
    private string _hintText = string.Empty;
    private ObsConnectionManagerState _obsStatus = ObsConnectionManagerState.Idle;
    private int _obsRetryAttempt;
    private RecordingCoordinator? _coordinator;
    private GameId _selectedGame = GameId.Infinitas;

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        ILogger<MainWindowViewModel>? logger = null)
        : this(stateMachine, clipboard, dialog, recordStore, AppSettings.CreateDefault(), null, null, null, null, logger) { }

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        AppSettings settings,
        SettingsStore? settingsStore,
        string? settingsPath,
        ILogger<MainWindowViewModel>? logger = null)
        : this(stateMachine, clipboard, dialog, recordStore, settings, settingsStore, settingsPath, null, null, logger) { }

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        AppSettings settings,
        SettingsStore? settingsStore,
        string? settingsPath,
        IGitHubReleaseChecker? releaseChecker,
        ILogger<MainWindowViewModel>? logger = null)
        : this(stateMachine, clipboard, dialog, recordStore, settings, settingsStore, settingsPath, releaseChecker, null, logger) { }

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        IDialogService dialog,
        JsonRecordStore recordStore,
        AppSettings settings,
        SettingsStore? settingsStore,
        string? settingsPath,
        IGitHubReleaseChecker? releaseChecker,
        IUpdateService? updateService,
        ILogger<MainWindowViewModel>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _settings = settings ?? AppSettings.CreateDefault();
        _settingsStore = settingsStore;
        _settingsPath = settingsPath;
        _releaseChecker = releaseChecker;
        _updateService = updateService;
        _selectedGame = _settings.ResolveSelectedGame();
        _record.Game = _selectedGame;
        _format = _settings.TimestampFormatFor(_selectedGame);
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
        CheckLatestVersionCommand = new RelayCommand(ExecuteCheckLatestVersion, () => _releaseChecker is not null);
    }

    public ObservableCollection<TimestampViewModel> Timestamps { get; } = new();

    public AppState State => _stateMachine.State;

    /// <summary>ゲーム選択コンボの選択肢。</summary>
    public IReadOnlyList<GameChoice> AvailableGames { get; } =
        GameCatalog.AllGames.Select(g => new GameChoice(g, GameCatalog.DisplayName(g))).ToList();

    /// <summary>
    /// 記録対象のゲーム。1 配信 = 1 ゲーム（JSON の <c>game</c>）なので、
    /// 記録を始めたあとは変更できない（<see cref="IsGameSelectable"/>）。
    /// </summary>
    public GameId SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (_selectedGame == value) return;
            if (!IsGameSelectable)
            {
                // 記録中に切り替わると record.game と検知内容が食い違うため拒否し、表示を戻す
                RaisePropertyChanged(nameof(SelectedGame));
                return;
            }

            _selectedGame = value;
            _record.Game = value;
            RaisePropertyChanged(nameof(SelectedGame));

            // 識別子セットがゲームごとに違うため、フォーマットもゲーム別のものへ切り替える
            Format = _settings.TimestampFormatFor(value);
            ApplySettingsToCoordinator();
            PersistSettings();
        }
    }

    /// <summary>ゲーム選択が可能か。初期状態でのみ変更できる。</summary>
    public bool IsGameSelectable => State == AppState.Initial;

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

    /// <summary>
    /// 記録操作ボタンの隣のボタン。既定は「リセット」だが、`配信開始待ち` では
    /// 「停止」として配信開始待ちの解除（→ `初期状態`）に使う。
    /// 記録操作ボタンがその状態では「強制開始」に変わるため、待機をやめる導線が他に無い。
    /// </summary>
    public string SecondaryButtonText => State == AppState.WaitingForStream ? "停止" : "リセット";

    public RelayCommand SecondaryCommand => State == AppState.WaitingForStream ? StopCommand : ResetCommand;

    public string SecondaryHintText => State == AppState.WaitingForStream
        ? "配信開始待ちを解除して初期状態に戻します"
        : "現在の記録をリセットします（記録終了状態のみ）";

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
    public RelayCommand CheckLatestVersionCommand { get; }

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

        _coordinator.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = streamObs,
            Game = _selectedGame,
            WatchDirectory = _settings.WatchDirectoryFor(_selectedGame),
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

    private async void ExecuteCheckLatestVersion()
    {
        await CheckLatestVersionAsync(silent: false).ConfigureAwait(true);
    }

    public async Task CheckLatestVersionAsync(bool silent, CancellationToken cancellationToken = default)
    {
        if (_releaseChecker is null)
        {
            if (!silent)
                _dialog.ShowError("最新バージョン情報", "バージョンチェックサービスが利用できません。");
            return;
        }

        GitHubRelease? release;
        try
        {
            release = await _releaseChecker.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "最新バージョン情報の取得に失敗しました。");
            if (!silent)
                _dialog.ShowError("最新バージョン情報", "最新バージョン情報の取得に失敗しました。");
            return;
        }

        if (release is null)
        {
            if (!silent)
                _dialog.ShowError("最新バージョン情報", "最新バージョン情報の取得に失敗しました。");
            return;
        }

        var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        var hasNewer = VersionComparer.IsNewer(release.TagName, current);

        if (hasNewer)
        {
            // Velopack でインストール済みなら自動アップデートフロー、そうでなければリリースページ起動
            if (_updateService?.IsInstalled == true)
            {
                var confirmed = _dialog.Confirm(
                    "新しいバージョンが利用可能",
                    $"新しいバージョン {release.TagName} が利用可能です。\n（現在: v{current})\n\nダウンロードして適用しますか？\n（完了後、自動的に再起動します）");
                if (!confirmed) return;

                try
                {
                    var success = await _dialog.ShowUpdateProgressAsync(_updateService, cancellationToken).ConfigureAwait(true);
                    if (success)
                    {
                        _updateService.ApplyAndRestart();
                    }
                    else if (!silent)
                    {
                        _dialog.ShowError("アップデート", "アップデートの取得に失敗しました。");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "セルフアップデートに失敗しました。");
                    if (!silent)
                        _dialog.ShowError("アップデート", "アップデートの適用に失敗しました: " + ex.Message);
                }
            }
            else
            {
                var confirmed = _dialog.Confirm(
                    "新しいバージョンが利用可能",
                    $"新しいバージョン {release.TagName} がリリースされています。\n（現在: v{current})\n\nリリースページを開きますか？");
                if (confirmed)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "リリースページの起動に失敗しました。");
                        if (!silent)
                            _dialog.ShowError("リリースページ", "ブラウザの起動に失敗しました: " + ex.Message);
                    }
                }
            }
        }
        else if (!silent)
        {
            _dialog.ShowInfo("最新バージョン情報", "現在のバージョンが最新です。");
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
            _record = new StreamRecord { Game = _selectedGame };
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

        // 選択中ゲームのフォーマット文字列を即時反映
        Format = _settings.TimestampFormatFor(_selectedGame);

        // OBS 接続情報や監視ディレクトリが変わった可能性があるので Coordinator にも反映
        ApplySettingsToCoordinator();

        PersistSettings();
    }

    private void PersistSettings()
    {
        if (_settingsStore is null || string.IsNullOrEmpty(_settingsPath)) return;

        _settings.General ??= new GeneralSettings { BackupDirectory = AppSettings.DefaultBackupDirectory() };
        _settings.General.SelectedGame = _selectedGame.ToSerializedString();

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

        // ファイルのゲームに合わせる（読込後は記録終了状態なので選択コンボは触れない）
        if (_selectedGame != record.Game)
        {
            _selectedGame = record.Game;
            RaisePropertyChanged(nameof(SelectedGame));
            Format = _settings.TimestampFormatFor(_selectedGame);
            ApplySettingsToCoordinator();
        }

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
        RaisePropertyChanged(nameof(SecondaryButtonText));
        RaisePropertyChanged(nameof(SecondaryCommand));
        RaisePropertyChanged(nameof(SecondaryHintText));
        RaisePropertyChanged(nameof(CanReset));
        RaisePropertyChanged(nameof(IsGameSelectable));

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

/// <summary>ゲーム選択コンボの 1 項目（enum を表示名付きで見せるための入れ物）。</summary>
public sealed record GameChoice(GameId Id, string DisplayName);
