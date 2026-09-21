using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Sdvx;

/// <summary>
/// SDVX Helper のログ（<c>log/sdvx_helper.log</c>）を追いかけて、画面が「プレイ中」に入ったことを検知する。
///
/// SDVX Helper v2 の WebSocket にはプレイ開始そのもののイベントが無く、曲決定画面の <c>nowplaying</c> で
/// 代用すると **リザルト画面からのリトライ**（選曲も曲決定も通らない）と **曲決定画面の取りこぼし**
/// （SDVX Helper の画面判定が <c>select → play</c> と直接遷移する）を落とす。
/// 一方 SDVX Helper は画面遷移のたびに <c>モード変更: select → play</c> のような行をログへ書いており、
/// これはどちらのケースでも出るので、こちらを主のプレイ開始信号にする。
///
/// ログは <c>RotatingFileHandler</c>（2MB でローテーション）で書かれる。追記分だけを読み、
/// サイズが縮んだらローテーションとみなして先頭から読み直す。
/// </summary>
public sealed class SdvxHelperLogTail : IDisposable
{
    public const string LogDirectoryName = "log";
    public const string LogFileName = "sdvx_helper.log";

    /// <summary>SDVX Helper の <c>detect_mode</c> のうち、プレイ画面を表す名前。</summary>
    public const string PlayMode = "play";

    // 例: [2026-09-22 20:15:30,123] [INFO] sdvx_helper.pyw:781:_on_mode_changed - モード変更: select → play
    private static readonly Regex ModeChangePattern = new(
        @"モード変更:\s*(?<from>\S+)\s*(?:→|->)\s*(?<to>\S+)",
        RegexOptions.Compiled);

    private static readonly Regex TimestampPattern = new(
        @"^\[(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})(?:,(?<ms>\d{3}))?\]",
        RegexOptions.Compiled);

    /// <summary>FileSystemWatcher が追記を取りこぼしたときの保険としてのポーリング間隔。</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly StringBuilder _partialLine = new();

    private string _path = string.Empty;
    private long _offset;
    private FileSystemWatcher? _watcher;
    private Timer? _pollTimer;
    private bool _disposed;

    public SdvxHelperLogTail()
        : this(NullLogger.Instance) { }

    public SdvxHelperLogTail(ILogger logger)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>プレイ画面に入った。引数はログに書かれた時刻（読めなければ検知時刻）。</summary>
    public event EventHandler<DateTimeOffset>? PlayEntered;

    public bool IsRunning
    {
        get { lock (_gate) return _watcher is not null; }
    }

    /// <summary>SDVX Helper のフォルダからログファイルのパスを組み立てる。</summary>
    public static string ResolveLogPath(string helperDirectory)
        => Path.Combine(helperDirectory, LogDirectoryName, LogFileName);

    /// <summary>
    /// 監視を開始する。ログファイルがまだ無くても（SDVX Helper 未起動）監視は張り、
    /// 作られた時点から読む。フォルダ自体が無ければ例外。
    /// </summary>
    public void Start(string helperDirectory)
    {
        if (string.IsNullOrWhiteSpace(helperDirectory))
            throw new ArgumentException("SDVX Helper のフォルダが指定されていません。", nameof(helperDirectory));

        var path = ResolveLogPath(helperDirectory);
        var directory = Path.GetDirectoryName(path)!;

        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SdvxHelperLogTail));
            if (_watcher is not null) throw new InvalidOperationException("既にログ監視が動作中です。");

            if (!System.IO.Directory.Exists(helperDirectory))
                throw new DirectoryNotFoundException($"SDVX Helper のフォルダが存在しません: {helperDirectory}");

            // log/ は SDVX Helper が起動時に作る。無ければ作っておかないと FileSystemWatcher が張れない
            System.IO.Directory.CreateDirectory(directory);

            _path = path;
            _partialLine.Clear();
            // 過去の行は再生しない（起動前のプレイを拾わない）
            _offset = File.Exists(path) ? new FileInfo(path).Length : 0;

            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            watcher.Filters.Add(LogFileName);
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;

            _pollTimer = new Timer(_ => Poll(), null, PollInterval, PollInterval);

            _logger.LogInformation("SDVX Helper のログ監視を開始します: {Path}（{Offset} バイト目から）", path, _offset);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_watcher is null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileEvent;
            _watcher.Created -= OnFileEvent;
            _watcher.Renamed -= OnFileEvent;
            _watcher.Dispose();
            _watcher = null;

            _pollTimer?.Dispose();
            _pollTimer = null;

            _logger.LogInformation("SDVX Helper のログ監視を停止しました。");
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => Poll();

    /// <summary>追記分を読んで処理する。テストからも直接呼ぶ。</summary>
    internal void Poll()
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(_path)) return;

            try
            {
                if (!File.Exists(_path)) return;

                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length < _offset)
                {
                    // ローテーションで新しいファイルに置き換わった
                    _logger.LogDebug("SDVX Helper のログがローテーションされたため先頭から読み直します。");
                    _offset = 0;
                    _partialLine.Clear();
                }

                if (stream.Length == _offset) return;

                stream.Seek(_offset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var appended = reader.ReadToEnd();
                _offset = stream.Length;

                ProcessText(appended);
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "SDVX Helper のログの読込に失敗しました（次回に再試行）。");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogDebug(ex, "SDVX Helper のログの読込に失敗しました（次回に再試行）。");
            }
        }
    }

    /// <summary>追記されたテキストを行に分けて処理する。最後の行が途中なら次回まで持ち越す。</summary>
    internal void ProcessText(string appended)
    {
        if (string.IsNullOrEmpty(appended)) return;

        _partialLine.Append(appended);
        var buffer = _partialLine.ToString();
        _partialLine.Clear();

        var lastNewline = buffer.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            _partialLine.Append(buffer);
            return;
        }

        var complete = buffer[..lastNewline];
        _partialLine.Append(buffer[(lastNewline + 1)..]);

        foreach (var rawLine in complete.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;
            ProcessLine(line);
        }
    }

    private void ProcessLine(string line)
    {
        var match = ModeChangePattern.Match(line);
        if (!match.Success) return;

        var to = match.Groups["to"].Value;
        if (!string.Equals(to, PlayMode, StringComparison.OrdinalIgnoreCase)) return;

        var at = TryParseLogTime(line, out var logged) ? logged : DateTimeOffset.Now;
        _logger.LogDebug("SDVX Helper のログでプレイ画面への遷移を検知: {From} → {To}", match.Groups["from"].Value, to);
        PlayEntered?.Invoke(this, at);
    }

    /// <summary>行頭の <c>[yyyy-MM-dd HH:mm:ss,fff]</c>（ローカル時刻）を読む。</summary>
    internal static bool TryParseLogTime(string line, out DateTimeOffset value)
    {
        value = default;
        var match = TimestampPattern.Match(line);
        if (!match.Success) return false;

        if (!DateTime.TryParseExact(match.Groups["time"].Value, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        if (match.Groups["ms"].Success
            && int.TryParse(match.Groups["ms"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
            parsed = parsed.AddMilliseconds(ms);

        value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
        return true;
    }

    /// <summary>テスト用: FileSystemWatcher を張らずに監視対象を設定する。</summary>
    internal void ConfigureForTest(string logPath, long offset = 0)
    {
        lock (_gate)
        {
            _path = logPath;
            _offset = offset;
            _partialLine.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
