using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
using InfTimestamper.Core.YouTube;
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

    /// <summary>実行中のバージョン。csproj の <c>&lt;Version&gt;</c> 由来でプレリリース表記（<c>-alpha.1</c>）を含む。</summary>
    public static SemanticVersion CurrentVersion { get; } = SemanticVersion.FromAssembly(Assembly.GetExecutingAssembly());

    private readonly AppStateMachine _stateMachine;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialog;
    private readonly JsonRecordStore _recordStore;
    private readonly SettingsStore? _settingsStore;
    private readonly string? _settingsPath;
    private readonly IGitHubReleaseChecker? _releaseChecker;
    private readonly IUpdateService? _updateService;
    private readonly IFileRecycler? _fileRecycler;
    private readonly YouTubeDescriptionSync? _youTubeSync;
    private readonly ILogger<MainWindowViewModel> _logger;

    private AppSettings _settings = AppSettings.CreateDefault();
    private StreamRecord _record = new();
    private string _format = DefaultFormat;
    private string _hintText = string.Empty;
    private ObsConnectionManagerState _obsStatus = ObsConnectionManagerState.Idle;
    private int _obsRetryAttempt;
    private RecordingCoordinator? _coordinator;
    private GameId _selectedGame = GameId.Infinitas;
    private StreamStartRowViewModel? _streamStartRow;

    /// <summary>自動バックアップの書込先。記録開始時／過去記録読込時に確定し、リセットで破棄する。</summary>
    private string? _backupPath;

    /// <summary>最後の保存以降に記録が変更されたか。終了確認ダイアログの要否判定に使う。</summary>
    private bool _isDirty;

    /// <summary>直近の自動バックアップが失敗しているか（状態ラベルに `保存エラー` を出す）。</summary>
    private bool _backupSaveFailed;

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
        ILogger<MainWindowViewModel>? logger = null,
        IFileRecycler? fileRecycler = null,
        YouTubeDescriptionSync? youTubeSync = null)
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
        _fileRecycler = fileRecycler;
        _youTubeSync = youTubeSync;
        _selectedGame = _settings.ResolveSelectedGame();
        _record.Game = _selectedGame;
        _format = _settings.TimestampFormatFor(_selectedGame);
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;

        _stateMachine.StateChanged += OnStateMachineChanged;
        Timestamps.CollectionChanged += OnTimestampsChanged;
        if (_youTubeSync is not null)
            _youTubeSync.StatusChanged += OnYouTubeSyncStatusChanged;

        StartCommand = new RelayCommand(ExecuteStart, () => State == AppState.Initial);
        ForceStartCommand = new RelayCommand(ExecuteForceStart, () => State == AppState.WaitingForStream);
        StopCommand = new RelayCommand(ExecuteStop,
            () => State is AppState.WaitingForStream or AppState.Recording);
        ResumeCommand = new RelayCommand(ExecuteResume, () => State == AppState.RecordingEnded);
        ResetCommand = new RelayCommand(ExecuteReset, () => State == AppState.RecordingEnded);
        CopyCommand = new RelayCommand(ExecuteCopy, () => Timestamps.Count > 0);

        // 配信開始時間が "-" のままでも、記録があるなら編集で入れ直せるようにする
        // （OBS 検知の取りこぼし等で基準時刻が欠けた記録を救済する導線）
        EditStreamStartedAtCommand = new RelayCommand(ExecuteEditStreamStartedAt,
            () => StreamStartedAt is not null || Timestamps.Count > 0);
        EditSelectedTimestampsCommand = new RelayCommand(ExecuteEditSelectedTimestamps,
            () => Timestamps.Any(t => t.IsSelected));
        OpenRecordCommand = new RelayCommand(ExecuteOpenRecord, () => State == AppState.Initial);
        SaveRecordCommand = new RelayCommand(ExecuteSaveRecord,
            () => Timestamps.Count > 0 || StreamStartedAt is not null);
        OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);

        ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
        OpenGitHubCommand = new RelayCommand(ExecuteOpenGitHub);
        CheckLatestVersionCommand = new RelayCommand(ExecuteCheckLatestVersion, () => _releaseChecker is not null);

        RefreshStreamStartRow();
    }

    public ObservableCollection<TimestampViewModel> Timestamps { get; } = new();

    /// <summary>
    /// 画面のタイムスタンプリストとクリップボードコピーの実体。
    /// <see cref="Timestamps"/>（プレイ記録のみ）の先頭に「配信開始」行を差し込んだもので、
    /// <see cref="Timestamps"/> 側の変更に追従して同期される。
    /// </summary>
    public ObservableCollection<ITimestampRow> DisplayRows { get; } = new();

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
            // 保存先に書けていないことはユーザ操作を要するので最優先で出す
            if (_backupSaveFailed)
                return "保存エラー";

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

    /// <summary>YouTube 連携の状態表示を出すか（設定で有効にしている場合のみ）。</summary>
    public bool IsYouTubeStatusVisible => _youTubeSync is not null && _settings.YouTube?.Enabled == true;

    /// <summary>YouTube の概要欄の同期状態。ダイアログは出さず、ここに簡潔に出すだけにする。</summary>
    public string YouTubeStatusText
    {
        get
        {
            if (!IsYouTubeStatusVisible) return string.Empty;

            var status = _youTubeSync!.Status;
            return status.State switch
            {
                YouTubeSyncState.Searching => "配信を検索中",
                YouTubeSyncState.Linked when status.LastUpdatedAt is { } at
                    => $"{at.ToLocalTime():HH:mm:ss} に更新（{status.BroadcastTitle}）",
                YouTubeSyncState.Linked => $"連携中（{status.BroadcastTitle}）",
                YouTubeSyncState.NotFound => "対象の配信が見つかりません",
                YouTubeSyncState.Error => "更新に失敗（再試行します）",
                YouTubeSyncState.QuotaExceeded => "API の上限に達したため停止",
                YouTubeSyncState.SignInRequired => "再ログインが必要です",
                _ => _youTubeSync.IsAvailable ? "待機中" : "未ログイン",
            };
        }
    }

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
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
        MarkDirtyAndSaveBackup();
    }

    public void SetStreamStartedAt(DateTimeOffset startedAt)
    {
        // 記録ファイルは秒精度なので、メモリ上の値も丸めて読み直しとズレないようにする
        startedAt = startedAt.TruncateToSecond();
        _record.Stream.StartedAt = startedAt;
        RaisePropertyChanged(nameof(StreamStartedAt));
        RaisePropertyChanged(nameof(StreamStartedAtText));
        foreach (var ts in Timestamps)
            ts.UpdateStreamStartedAt(startedAt);
        EditStreamStartedAtCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
        RefreshStreamStartRow();
        MarkDirtyAndSaveBackup();
    }

    public void NotifySelectionChanged()
        => EditSelectedTimestampsCommand.RaiseCanExecuteChanged();

    /// <summary>
    /// 行の選択状態が変わったら「選択項目の日時を編集」の可否を引き直す。
    /// <see cref="RelayCommand"/> は <c>CommandManager</c> を使わないので、明示的に通知しないと
    /// チェックボックスを操作してもメニューが有効化されない。
    /// </summary>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimestampViewModel.IsSelected))
            NotifySelectionChanged();
    }

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
            WatchTarget = _settings.WatchTargetFor(_selectedGame),
        });
    }

    private void OnPlayStarted(object? sender, PlayStartedEventArgs e)
    {
        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = e.CapturedAt.TruncateToSecond(),
        };
        foreach (var (key, value) in e.Fields)
            SetEntryField(entry, key, value);
        AddTimestamp(entry);

        _logger.LogInformation(
            "タイムスタンプを追加しました（{At:HH:mm:ss}、{Count} 件目、フィールド: {Fields}）。",
            entry.PlayStartedAt, Timestamps.Count, DescribeFields(e.Fields));
    }

    private void OnPlayResultDetected(object? sender, PlayResultEventArgs e)
    {
        // 直近のプレイ開始エントリに結果フィールドをマージ
        var latestEntry = _record.Timestamps.LastOrDefault();
        if (latestEntry is null)
        {
            _logger.LogWarning("プレイリザルトを検知しましたが、対象のタイムスタンプがありません。");
            return;
        }

        foreach (var (key, value) in e.Fields)
            SetEntryField(latestEntry, key, value);

        var vm = Timestamps.FirstOrDefault(t => ReferenceEquals(t.Entry, latestEntry));
        vm?.NotifyEntryUpdated();
        MarkDirtyAndSaveBackup();

        _logger.LogInformation(
            "タイムスタンプ（{At:HH:mm:ss}）にプレイリザルトをマージしました（フィールド: {Fields}）。",
            latestEntry.PlayStartedAt, DescribeFields(e.Fields));
    }

    /// <summary>ログ用に「key=value」を並べる。値が長くなりすぎないよう件数だけ落とさない。</summary>
    private static string DescribeFields(IReadOnlyDictionary<string, string> fields)
        => fields.Count == 0
            ? "(なし)"
            : string.Join(", ", fields.Select(pair => $"{pair.Key}={pair.Value}"));

    /// <summary>
    /// 検知結果を 1 フィールド書き込む。プレイ監視は値を文字列で渡してくるので、
    /// 数値型の識別子（要件「数値型フィールドは数値のまま保存」）はここで数値へ寄せる。
    /// </summary>
    private static void SetEntryField(TimestampEntry entry, string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (FieldKeys.IsNumeric(key)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            entry.SetField(key, number);
            return;
        }

        entry.SetField(key, value);
    }

    private void OnObsStatusChanged(object? sender, RecordingObsStatusChangedEventArgs e)
    {
        _obsStatus = e.State;
        _obsRetryAttempt = e.RetryAttempt;
        RaisePropertyChanged(nameof(StateLabel));
    }

    private void ExecuteShowAbout()
    {
        _dialog.ShowInfo("バージョン情報",
            $"INF-TIMESTAMPER\nバージョン: {CurrentVersion}\n\nGitHub: {GitHubUrl}");
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
            // α 版を動かしているときはプレリリースも見る（次の α 版へ更新できるように）。
            // 正式版の利用者には releases/latest（プレリリースを含まない）だけを見せる
            release = await _releaseChecker
                .GetLatestReleaseAsync(CurrentVersion.IsPrerelease, cancellationToken)
                .ConfigureAwait(true);
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

        var current = CurrentVersion;
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
        // 自動バックアップ済みの記録で警告を出さないよう、件数ではなく未保存フラグで判定する
        if (!isRecording && !_isDirty) return true;

        var message = isRecording
            ? "記録中の状態でアプリを終了しようとしています。終了しますか？"
            : "未保存の記録があります。終了しますか？";
        return _dialog.Confirm("終了確認", message);
    }

    /// <summary>
    /// アプリ終了時の最終保存。失敗した場合はダイアログで通知する
    /// （要件「アプリ終了時の最終保存失敗」）。
    /// </summary>
    public void SaveOnExit()
    {
        if (!_isDirty || string.IsNullOrEmpty(_backupPath)) return;

        if (!TrySaveBackup())
        {
            _dialog.ShowError("保存エラー",
                "保存先ディレクトリへの書込に失敗しました。保存先を変更してください");
        }
    }

    /// <summary>
    /// 起動時の異常終了復旧。`stream.endedAt == null` のファイルを新しい順に提示し、
    /// 読み込む／無視する／ゴミ箱へ送る をユーザに選ばせる（要件「異常終了からの復旧」）。
    /// </summary>
    public void CheckUnfinishedRecords()
    {
        var dir = _settings.General?.BackupDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return;

        List<UnfinishedRecord> unfinished;
        try
        {
            // 列挙途中の I/O 失敗を拾いたいので、この場でリスト化する
            unfinished = _recordStore.FindUnfinished(dir).ToList();
        }
        catch (Exception ex)
        {
            // アクセス権がない場合などは復旧チェックをスキップして起動を続ける
            _logger.LogWarning(ex, "未完了ファイルのスキャンに失敗しました。");
            return;
        }

        foreach (var candidate in unfinished)
        {
            var choice = _dialog.ConfirmUnfinishedRecord(candidate);

            if (choice == UnfinishedRecordChoice.Delete)
            {
                DeleteUnfinishedRecord(candidate);
                continue;
            }

            // 無視する場合は次回起動時にも再度提示される
            if (choice == UnfinishedRecordChoice.Ignore) continue;

            try
            {
                LoadRecord(candidate.Record, candidate.FilePath);
                return; // 読み込むのは 1 件だけ
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "未完了ファイルの読込に失敗しました。");
                _dialog.ShowError("読込エラー", ex.Message);
                return;
            }
        }
    }

    private void DeleteUnfinishedRecord(UnfinishedRecord candidate)
    {
        if (_fileRecycler is null)
        {
            _logger.LogWarning("ゴミ箱への削除機構が利用できないため、{Path} を削除しませんでした。", candidate.FilePath);
            return;
        }

        try
        {
            _fileRecycler.SendToRecycleBin(candidate.FilePath);
            _logger.LogInformation("未完了ファイルをゴミ箱へ送りました: {Path}", candidate.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "未完了ファイルの削除に失敗しました: {Path}", candidate.FilePath);
            _dialog.ShowError("削除エラー", "ファイルの削除に失敗しました: " + ex.Message);
        }
    }

    internal StreamRecord Record => _record;

    private void ExecuteStart()
    {
        TryStateOp(() => _stateMachine.Start(), "開始");
    }

    // 配信開始/終了時刻の確定は OnStateMachineChanged に集約している。
    // ボタン操作と OBS の検知イベントで別々に書いていたため、OBS 検知経由だけ
    // stream.startedAt / endedAt が入らない不具合になっていた。
    private void ExecuteForceStart()
    {
        TryStateOp(() => _stateMachine.ForceStart(), "強制開始");
    }

    private void ExecuteStop()
    {
        TryStateOp(() => _stateMachine.Stop(), "停止");
    }

    private void ExecuteResume()
    {
        TryStateOp(() => _stateMachine.Resume(), "再開");
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
            _backupPath = null;
            _isDirty = false;
            ClearBackupSaveError();
            Timestamps.Clear();
            RefreshStreamStartRow();
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
        // 未設定（"-" 表示）なら、最初のプレイ記録の時刻を初期値にして編集させる
        var seed = StreamStartedAt
            ?? Timestamps.FirstOrDefault()?.PlayStartedAt
            ?? DateTimeOffset.Now;

        var result = _dialog.ShowDateTimeEditor(new[] { seed });
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
            selected[i].Entry.PlayStartedAt = result[i].TruncateToSecond();
            selected[i].NotifyEntryUpdated();
        }
        ReorderTimestamps();
        MarkDirtyAndSaveBackup();
    }

    private void ExecuteOpenRecord()
    {
        var path = _dialog.ShowOpenFileDialog(JsonFileFilter);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var record = _recordStore.Load(path);
            LoadRecord(record, path);
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

        // 配信開始行の有無・文言が変わった可能性がある
        RefreshStreamStartRow();

        // 保存先を設定し直した場合、記録中でも次の保存タイミングから書けるようにする
        if (State is AppState.Recording or AppState.RecordingEnded)
        {
            EnsureBackupPath();
            if (_isDirty) TrySaveBackup();
        }

        // OBS 接続情報や監視ディレクトリが変わった可能性があるので Coordinator にも反映
        ApplySettingsToCoordinator();

        // YouTube 連携の有効 / 無効・見出し行・フォーマットの変更を記録中の同期へ反映
        ApplyYouTubeSettings();

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
            _isDirty = false;
            _dialog.ShowInfo("保存完了", $"記録を保存しました:\n{path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存に失敗しました。");
            _dialog.ShowError("保存エラー", ex.Message);
        }
    }

    private void LoadRecord(StreamRecord record, string? sourcePath)
    {
        if (_stateMachine.State == AppState.Initial)
            _stateMachine.OpenFile();

        _record = record;

        // 「記録再開」で続きを記録した場合も同じファイルへ書き戻す
        _backupPath = string.IsNullOrEmpty(sourcePath) ? null : sourcePath;
        _isDirty = false;
        ClearBackupSaveError();

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

        RefreshStreamStartRow();
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
        foreach (var row in DisplayRows)
            sb.AppendLine(row.DisplayText);
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
        ApplyRecordTransition(e.OldState, e.NewState);

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
        if (e.OldItems is not null)
        {
            foreach (TimestampViewModel removed in e.OldItems)
                removed.PropertyChanged -= OnRowPropertyChanged;
        }
        if (e.NewItems is not null)
        {
            foreach (TimestampViewModel added in e.NewItems)
            {
                // 並べ替え（Clear → 再追加）で同じインスタンスが戻ってくるので、二重購読を避ける
                added.PropertyChanged -= OnRowPropertyChanged;
                added.PropertyChanged += OnRowPropertyChanged;
            }
        }

        SyncDisplayRows(e);
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
        SaveRecordCommand.RaiseCanExecuteChanged();
        EditSelectedTimestampsCommand.RaiseCanExecuteChanged();
        EditStreamStartedAtCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 「配信開始待ち → 記録中」等の遷移に伴う記録データ側の副作用。
    /// ボタン操作でも OBS の配信開始/終了検知でも同じ経路を通るように、
    /// 状態機械のイベント 1 箇所にまとめている。
    /// </summary>
    private void ApplyRecordTransition(AppState oldState, AppState newState)
    {
        if (oldState == AppState.WaitingForStream && newState == AppState.Recording)
        {
            // 記録中への遷移時刻が相対タイムスタンプの基準になる
            if (_record.Stream.StartedAt == default)
                SetStreamStartedAt(DateTimeOffset.Now);
            _record.Stream.EndedAt = null;
            EnsureBackupPath();
            MarkDirtyAndSaveBackup();
            BeginYouTubeSync();
        }
        else if (oldState == AppState.RecordingEnded && newState == AppState.Recording)
        {
            // 記録再開。終了時刻を取り消して続きを記録する
            _record.Stream.EndedAt = null;
            EnsureBackupPath();
            MarkDirtyAndSaveBackup();
            // 過去記録の再開でも、そのライブがまだ配信中なら同期される（配信中のライブだけが対象）
            BeginYouTubeSync();
        }
        else if (oldState == AppState.Recording && newState == AppState.RecordingEnded)
        {
            _record.Stream.EndedAt = DateTimeOffset.Now.TruncateToSecond();
            MarkDirtyAndSaveBackup();
            EndYouTubeSync(writeFinal: true);
        }
    }

    // --- YouTube の概要欄の同期 -------------------------------------------

    private YouTubeSyncOptions BuildYouTubeSyncOptions()
    {
        var youTube = _settings.YouTube ?? new YouTubeSettings();
        return new YouTubeSyncOptions(
            youTube.ResolveHeading(),
            youTube.ResolveUpdateInterval(),
            YouTubeBroadcastMatcher.DefaultTolerance);
    }

    /// <summary>概要欄に書く行。コピー結果と同じ（配信開始行を含む）。</summary>
    private List<string> BuildYouTubeLines() => DisplayRows.Select(r => r.DisplayText).ToList();

    private void BeginYouTubeSync()
    {
        if (_youTubeSync is null || _settings.YouTube?.Enabled != true) return;
        if (_record.Stream.StartedAt == default) return;

        _youTubeSync.BeginSession(_record.Stream.StartedAt, BuildYouTubeSyncOptions());
        PushYouTubeUpdate();
    }

    /// <summary>記録中なら概要欄の更新を依頼する（書き込みは同期側で間引かれる）。</summary>
    private void PushYouTubeUpdate()
    {
        if (_youTubeSync is null || State != AppState.Recording || !_youTubeSync.IsActive) return;
        // プレイが 1 件も無いうちは「配信開始」行だけのブロックになるので書かない
        if (Timestamps.Count == 0) return;
        _youTubeSync.RequestUpdate(BuildYouTubeLines());
    }

    private async void EndYouTubeSync(bool writeFinal)
    {
        if (_youTubeSync is null || !_youTubeSync.IsActive) return;
        try
        {
            var finalLines = writeFinal && Timestamps.Count > 0 ? BuildYouTubeLines() : null;
            await _youTubeSync.EndSessionAsync(finalLines).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube の概要欄の同期の終了処理に失敗しました。");
        }
    }

    private void ApplyYouTubeSettings()
    {
        RaisePropertyChanged(nameof(IsYouTubeStatusVisible));
        RaisePropertyChanged(nameof(YouTubeStatusText));
        if (_youTubeSync is null || State != AppState.Recording) return;

        var enabled = _settings.YouTube?.Enabled == true;
        if (!enabled)
        {
            EndYouTubeSync(writeFinal: false);
            return;
        }

        // 記録中に有効化した・ログインし直した場合はここから同期を始める
        if (!_youTubeSync.IsActive || _youTubeSync.Status.State == YouTubeSyncState.SignInRequired)
        {
            BeginYouTubeSync();
            return;
        }

        _youTubeSync.UpdateOptions(BuildYouTubeSyncOptions());
        PushYouTubeUpdate();
    }

    private void OnYouTubeSyncStatusChanged(object? sender, YouTubeSyncStatus e)
        => RaisePropertyChanged(nameof(YouTubeStatusText));

    // --- 配信開始行 -------------------------------------------------------

    /// <summary>設定と配信開始時間の有無に合わせて、リスト先頭の「配信開始」行を出し入れする。</summary>
    private void RefreshStreamStartRow()
    {
        var enabled = _settings.General?.IncludeStreamStartRow != false && StreamStartedAt is not null;

        if (!enabled)
        {
            if (_streamStartRow is not null)
            {
                DisplayRows.Remove(_streamStartRow);
                _streamStartRow = null;
            }
            return;
        }

        var label = _settings.General?.StreamStartRowLabel;
        if (string.IsNullOrWhiteSpace(label))
            label = AppSettings.DefaultStreamStartRowLabel;

        if (_streamStartRow is null)
        {
            _streamStartRow = new StreamStartRowViewModel(label);
            DisplayRows.Insert(0, _streamStartRow);
        }
        else
        {
            _streamStartRow.Label = label;
        }
    }

    /// <summary><see cref="Timestamps"/> の変更を <see cref="DisplayRows"/> へ写す。</summary>
    private void SyncDisplayRows(NotifyCollectionChangedEventArgs e)
    {
        var offset = _streamStartRow is null ? 0 : 1;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null && e.NewStartingIndex >= 0:
                for (var i = 0; i < e.NewItems.Count; i++)
                    DisplayRows.Insert(e.NewStartingIndex + offset + i, (ITimestampRow)e.NewItems[i]!);
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null && e.OldStartingIndex >= 0:
                for (var i = e.OldItems.Count - 1; i >= 0; i--)
                    DisplayRows.RemoveAt(e.OldStartingIndex + offset + i);
                break;

            default:
                // Reset / Move / Replace や添字不明のケースは作り直す（件数が小さいので十分速い）
                RebuildDisplayRows();
                break;
        }
    }

    private void RebuildDisplayRows()
    {
        DisplayRows.Clear();
        if (_streamStartRow is not null)
            DisplayRows.Add(_streamStartRow);
        foreach (var ts in Timestamps)
            DisplayRows.Add(ts);
    }

    // --- 自動バックアップ -------------------------------------------------

    /// <summary>バックアップの書込先を確定する（既に決まっていれば何もしない）。</summary>
    private void EnsureBackupPath()
    {
        if (!string.IsNullOrEmpty(_backupPath)) return;

        var dir = _settings.General?.BackupDirectory;
        if (string.IsNullOrEmpty(dir)) return;

        var startedAt = _record.Stream.StartedAt == default ? DateTimeOffset.Now : _record.Stream.StartedAt;
        _backupPath = Path.Combine(dir, JsonRecordStore.GenerateFileName(_record.Game, startedAt));
    }

    private void MarkDirtyAndSaveBackup()
    {
        _record.UpdatedAt = DateTimeOffset.Now;
        _isDirty = true;
        TrySaveBackup();
        PushYouTubeUpdate();
    }

    /// <summary>
    /// バックアップへ上書き保存する。失敗しても記録は継続し、状態ラベルに `保存エラー` を出して
    /// 次の保存タイミングで再試行する（要件「ファイル I/O 失敗」）。
    /// </summary>
    private bool TrySaveBackup()
    {
        var path = _backupPath;
        if (string.IsNullOrEmpty(path)) return true;

        try
        {
            _recordStore.SaveAtomic(_record, path);
            _isDirty = false;
            ClearBackupSaveError();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "バックアップの保存に失敗しました: {Path}", path);
            if (!_backupSaveFailed)
            {
                _backupSaveFailed = true;
                RaisePropertyChanged(nameof(StateLabel));
            }
            return false;
        }
    }

    private void ClearBackupSaveError()
    {
        if (!_backupSaveFailed) return;
        _backupSaveFailed = false;
        RaisePropertyChanged(nameof(StateLabel));
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
