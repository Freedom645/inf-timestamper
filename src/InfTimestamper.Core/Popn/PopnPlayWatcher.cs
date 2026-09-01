using System.Text;
using System.Text.Json;
using InfTimestamper.Core.Games;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Popn;

/// <summary>
/// popn-lively-tracker が出力するファイル群を監視し、プレイ開始 / プレイリザルトを検知する。
///
/// INFINITAS の <c>RefluxPlayWatcher</c> と違い、**リザルトは状態遷移のエッジでは読めない**。
/// <c>result.json</c> は譜面の特定にレコード表の更新を待つため、リザルト画面の表示から数秒
/// （既定タイムアウト 20 秒）遅れて書かれる。エッジで読むと直前のプレイのリザルトを拾ってしまう。
/// そのため <c>state.txt</c> の「プレイ中」突入でプレイ開始を発火したあとはリザルト待ちに入り、
/// <c>result.json</c> 自身の書き換えを検知した時点で <see cref="PlayResultDetected"/> を発火する。
/// 書き出しは <c>.tmp</c> → <c>os.replace</c> のアトミック方式なので中途半端な内容は読まれない。
/// </summary>
public sealed class PopnPlayWatcher : IPlayWatcher
{
    public const string StateFileName = "state.txt";
    public const string ResultFileName = "result.json";

    public const string StateMusicSelect = "選曲画面";
    public const string StatePlaying = "プレイ中";
    public const string StatePlayEnded = "プレイ終了";
    public const string StateIdle = "待機";

    private const int ReadRetries = 3;
    private const int ReadRetryDelayMs = 50;

    /// <summary>
    /// <c>result.json</c> の <c>time</c> がプレイ開始より前なら前回のリザルトとみなして捨てる。
    /// time は秒精度なので、丸めで 1 秒手前に出るぶんだけ猶予を持たせる。
    /// </summary>
    private static readonly TimeSpan ResultTimeSlack = TimeSpan.FromSeconds(1);

    private readonly ILogger<PopnPlayWatcher> _logger;
    private readonly object _gate = new();

    private FileSystemWatcher? _watcher;
    private string _directory = string.Empty;
    private string _lastState = StateIdle;
    private DateTimeOffset? _awaitingResultSince;
    private bool _disposed;

    public PopnPlayWatcher()
        : this(NullLogger<PopnPlayWatcher>.Instance) { }

    public PopnPlayWatcher(ILogger<PopnPlayWatcher> logger)
    {
        _logger = logger ?? NullLogger<PopnPlayWatcher>.Instance;
    }

    /// <summary>プレイ開始検知。CapturedAt はエッジ検知時刻。曲情報はリザルトまで判らないため Fields は空。</summary>
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は result.json から展開した各識別子。</summary>
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public bool IsRunning
    {
        get { lock (_gate) return _watcher is not null; }
    }

    public void Start(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("popn-lively-tracker の出力ディレクトリが指定されていません。", nameof(directory));

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PopnPlayWatcher));
            if (_watcher is not null)
                throw new InvalidOperationException("既に監視が動作中です。");

            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("popn-lively-tracker の出力ディレクトリが存在しません: {Directory}", directory);
                throw new DirectoryNotFoundException($"popn-lively-tracker の出力ディレクトリが存在しません: {directory}");
            }

            _directory = directory;
            _lastState = StateIdle;
            _awaitingResultSince = null;

            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            watcher.Filters.Add(StateFileName);
            watcher.Filters.Add(ResultFileName);
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;

            _logger.LogInformation("popn-lively-tracker のファイル監視を開始します: {Directory}", directory);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_watcher is null) return Task.CompletedTask;
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
            _watcher = null;
            _lastState = StateIdle;
            _awaitingResultSince = null;
            _logger.LogInformation("popn-lively-tracker のファイル監視を停止しました。");
        }
        return Task.CompletedTask;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => Dispatch(e.Name);

    // .tmp からの置き換えは、環境によっては Renamed として飛んでくる
    private void OnFileRenamed(object sender, RenamedEventArgs e) => Dispatch(e.Name);

    private void Dispatch(string? name)
    {
        if (string.Equals(name, StateFileName, StringComparison.OrdinalIgnoreCase))
            ProcessState();
        else if (string.Equals(name, ResultFileName, StringComparison.OrdinalIgnoreCase))
            ProcessResult();
    }

    /// <summary>
    /// state.txt を読み、直前状態との差分でプレイ開始を判定する。
    /// 同一状態の連続通知はノーオペ（要件: エントリ操作は状態遷移エッジでのみ行う）。
    /// </summary>
    internal void ProcessState()
    {
        lock (_gate)
        {
            var state = _lastState;
            try
            {
                var dir = _directory;
                if (string.IsNullOrEmpty(dir)) return;

                state = ReadText(Path.Combine(dir, StateFileName), _lastState);
                if (state == _lastState) return;

                if (state == StatePlaying)
                {
                    // リザルトはこの時点では出ていないので、曲情報は空のままエントリを起こす
                    var startedAt = DateTimeOffset.Now;
                    _awaitingResultSince = startedAt;

                    _logger.LogDebug("pop'n: プレイ開始を検知しました。");
                    PlayStarted?.Invoke(this, new PlayStartedEventArgs(
                        startedAt, new Dictionary<string, string>()));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "popn-lively-tracker の状態ファイル処理に失敗しました。");
            }
            finally
            {
                _lastState = state;
            }
        }
    }

    /// <summary>
    /// result.json の書き換えを検知したときの処理。プレイ開始後に書かれたものだけを採用する。
    /// </summary>
    internal void ProcessResult()
    {
        lock (_gate)
        {
            try
            {
                var dir = _directory;
                if (string.IsNullOrEmpty(dir)) return;

                var awaitingSince = _awaitingResultSince;
                if (awaitingSince is null)
                {
                    // プレイを検知していない状態での書き換え（起動直後の残骸など）は無視する
                    _logger.LogDebug("pop'n: リザルト待ちでないため result.json の変化を無視します。");
                    return;
                }

                var json = ReadResultJson(Path.Combine(dir, ResultFileName));
                if (json is null) return;

                // time が読めた場合はプレイ開始より後のものだけを採用（直前プレイの取り違え防止）。
                // 読めない場合は判定材料が無いので採用する。
                if (PopnFieldMapper.TryParseTime(json.Time, out var resultTime)
                    && resultTime < awaitingSince.Value - ResultTimeSlack)
                {
                    _logger.LogDebug(
                        "pop'n: プレイ開始 {Started} より前の result.json ({ResultTime}) のため無視します。",
                        awaitingSince.Value, resultTime);
                    return;
                }

                _awaitingResultSince = null;

                var fields = PopnFieldMapper.Map(json);
                if (json.Music is null)
                {
                    _logger.LogInformation(
                        "pop'n: 譜面を特定できなかったリザルトです (identified_by={IdentifiedBy})。曲情報は空にします。",
                        json.IdentifiedBy ?? "-");
                }

                _logger.LogDebug("pop'n: プレイリザルトを検知 ({Count} フィールド)", fields.Count);
                PlayResultDetected?.Invoke(this, new PlayResultEventArgs(DateTimeOffset.Now, fields));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "popn-lively-tracker のリザルトファイル処理に失敗しました。");
            }
        }
    }

    private PopnResultJson? ReadResultJson(string path)
    {
        for (var attempt = 0; attempt < ReadRetries; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return JsonSerializer.Deserialize<PopnResultJson>(stream);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "result.json の解析に失敗しました: {Path}", path);
                return null;
            }
            catch (IOException) when (attempt < ReadRetries - 1)
            {
                Thread.Sleep(ReadRetryDelayMs);
            }
        }
        _logger.LogWarning("result.json の読込に失敗しました（ロック継続）: {Path}", path);
        return null;
    }

    private static string ReadText(string path, string defaultValue)
    {
        for (var attempt = 0; attempt < ReadRetries; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd().Trim();
            }
            catch (FileNotFoundException) { return defaultValue; }
            catch (DirectoryNotFoundException) { return defaultValue; }
            catch (IOException) when (attempt < ReadRetries - 1)
            {
                Thread.Sleep(ReadRetryDelayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < ReadRetries - 1)
            {
                Thread.Sleep(ReadRetryDelayMs);
            }
        }
        return defaultValue;
    }

    /// <summary>テスト用: FileSystemWatcher を介さずに監視対象ディレクトリを設定する。</summary>
    internal void ConfigureForTest(string directory)
    {
        _directory = directory;
        _lastState = StateIdle;
        _awaitingResultSince = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
