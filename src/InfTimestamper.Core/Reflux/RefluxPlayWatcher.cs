using System.Text;
using System.Text.Json;
using InfTimestamper.Core.Games;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Reflux;

/// <summary>
/// Reflux が出力するファイル群を監視し、プレイ開始 / プレイリザルトを検知して
/// <see cref="PlayStarted"/> / <see cref="PlayResultDetected"/> を発火する。
///
/// 前段 Python 実装 (reflux_file_watcher.py) の移植。
/// <c>playstate.txt</c> の変化を <see cref="FileSystemWatcher"/> で監視し、
/// <c>off/menu → play</c> でプレイ開始、<c>play → 非play</c> でリザルトとして扱う。
/// プレイ開始時は <c>title.txt</c> / <c>level.txt</c> を、リザルト時は <c>latest.json</c> を読む。
/// </summary>
public sealed class RefluxPlayWatcher : IPlayWatcher
{
    public const string PlayStateFileName = "playstate.txt";
    public const string TitleFileName = "title.txt";
    public const string LevelFileName = "level.txt";
    public const string LatestJsonFileName = "latest.json";

    public const string PlayStateOff = "off";
    public const string PlayStatePlay = "play";

    private const int ReadRetries = 3;
    private const int ReadRetryDelayMs = 50;
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    private readonly ILogger<RefluxPlayWatcher> _logger;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();

    private FileSystemWatcher? _watcher;
    private string _directory = string.Empty;
    private string _lastStatus = PlayStateOff;
    private bool _disposed;

    public RefluxPlayWatcher()
        : this(NullLogger<RefluxPlayWatcher>.Instance) { }

    public RefluxPlayWatcher(ILogger<RefluxPlayWatcher> logger)
        : this(logger, DefaultDebounce) { }

    public RefluxPlayWatcher(ILogger<RefluxPlayWatcher> logger, TimeSpan debounce)
    {
        _logger = logger ?? NullLogger<RefluxPlayWatcher>.Instance;
        _debounce = debounce < TimeSpan.Zero ? TimeSpan.Zero : debounce;
    }

    /// <summary>プレイ開始検知。CapturedAt はエッジ検知時刻、Fields は title/level。</summary>
    public event EventHandler<PlayStartedEventArgs>? PlayStarted;

    /// <summary>プレイリザルト検知。Fields は latest.json から展開した全フィールド。</summary>
    public event EventHandler<PlayResultEventArgs>? PlayResultDetected;

    public bool IsRunning
    {
        get { lock (_gate) return _watcher is not null; }
    }

    /// <summary>指定ディレクトリの監視を開始する。</summary>
    public void Start(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Reflux 出力ディレクトリが指定されていません。", nameof(directory));

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RefluxPlayWatcher));
            if (_watcher is not null)
                throw new InvalidOperationException("既に監視が動作中です。");

            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Reflux 出力ディレクトリが存在しません: {Directory}", directory);
                throw new DirectoryNotFoundException($"Reflux 出力ディレクトリが存在しません: {directory}");
            }

            _directory = directory;
            _lastStatus = PlayStateOff;

            var watcher = new FileSystemWatcher(directory, PlayStateFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            watcher.Changed += OnPlayStateFileChanged;
            watcher.Created += OnPlayStateFileChanged;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;

            _logger.LogInformation("Reflux ファイル監視を開始します: {Directory}", directory);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_watcher is null) return Task.CompletedTask;
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnPlayStateFileChanged;
            _watcher.Created -= OnPlayStateFileChanged;
            _watcher.Dispose();
            _watcher = null;
            _lastStatus = PlayStateOff;
            _logger.LogInformation("Reflux ファイル監視を停止しました。");
        }
        return Task.CompletedTask;
    }

    private void OnPlayStateFileChanged(object sender, FileSystemEventArgs e) => ProcessPlayState();

    /// <summary>
    /// playstate.txt を読み、直前状態との差分でプレイ開始 / リザルトを判定して通知する。
    /// FileSystemWatcher のイベントは連続発火しうるため、ロックで直列化し _lastStatus を保護する。
    /// </summary>
    internal void ProcessPlayState()
    {
        lock (_gate)
        {
            var status = _lastStatus;
            try
            {
                var dir = _directory;
                if (string.IsNullOrEmpty(dir)) return;

                status = ReadText(Path.Combine(dir, PlayStateFileName), _lastStatus).ToLowerInvariant();
                if (status == _lastStatus) return;

                // Reflux が title/level/latest.json を書き終えるのを待つ
                if (_debounce > TimeSpan.Zero)
                    Thread.Sleep(_debounce);

                if (status == PlayStatePlay)
                {
                    var title = ReadText(Path.Combine(dir, TitleFileName), "unknown");
                    var levelText = ReadText(Path.Combine(dir, LevelFileName), string.Empty);

                    var fields = new Dictionary<string, string>();
                    if (!string.IsNullOrWhiteSpace(title) && !title.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                        fields[FieldKeys.Title] = title;
                    if (int.TryParse(levelText.Trim(), out var level) && level >= 0)
                        fields[FieldKeys.Level] = level.ToString();

                    _logger.LogDebug("Reflux: プレイ開始を検知 (title={Title}, level={Level})", title, levelText);
                    PlayStarted?.Invoke(this, new PlayStartedEventArgs(DateTimeOffset.Now, fields));
                }
                else if (_lastStatus == PlayStatePlay)
                {
                    var fields = ReadLatestJson(Path.Combine(dir, LatestJsonFileName));
                    if (fields is not null)
                    {
                        _logger.LogDebug("Reflux: プレイリザルトを検知 ({Count} フィールド)", fields.Count);
                        PlayResultDetected?.Invoke(this, new PlayResultEventArgs(DateTimeOffset.Now, fields));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reflux ファイル監視の処理に失敗しました。");
            }
            finally
            {
                _lastStatus = status;
            }
        }
    }

    private IReadOnlyDictionary<string, string>? ReadLatestJson(string path)
    {
        for (var attempt = 0; attempt < ReadRetries; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var json = JsonSerializer.Deserialize<RefluxLatestJson>(stream);
                if (json is null) return null;
                return RefluxFieldMapper.Map(json);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "latest.json の解析に失敗しました: {Path}", path);
                return null;
            }
            catch (IOException) when (attempt < ReadRetries - 1)
            {
                Thread.Sleep(ReadRetryDelayMs);
            }
        }
        _logger.LogWarning("latest.json の読込に失敗しました（ロック継続）: {Path}", path);
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
        _lastStatus = PlayStateOff;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }
}
