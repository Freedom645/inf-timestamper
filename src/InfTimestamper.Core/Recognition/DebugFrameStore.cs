using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Recognition;

public sealed class DebugFrameStore
{
    public const int DefaultMaxFiles = 1000;
    public const string SubDirectoryName = "frames";

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly string _baseDir;
    private readonly bool _enabled;
    private readonly int _maxFiles;
    private readonly ILogger<DebugFrameStore> _logger;

    public DebugFrameStore(string logsDir, bool enabled, int maxFiles = DefaultMaxFiles)
        : this(logsDir, enabled, maxFiles, NullLogger<DebugFrameStore>.Instance) { }

    public DebugFrameStore(string logsDir, bool enabled, int maxFiles, ILogger<DebugFrameStore> logger)
    {
        if (string.IsNullOrWhiteSpace(logsDir))
            throw new ArgumentException("ログディレクトリが指定されていません。", nameof(logsDir));
        if (maxFiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFiles), maxFiles, "1 以上の値を指定してください。");

        _baseDir = Path.Combine(logsDir, SubDirectoryName);
        _enabled = enabled;
        _maxFiles = maxFiles;
        _logger = logger ?? NullLogger<DebugFrameStore>.Instance;
    }

    public bool IsEnabled => _enabled;

    public string BaseDirectory => _baseDir;

    public string? Save(byte[] pngBytes, DateTimeOffset capturedAt, string? reason = null)
    {
        if (!_enabled) return null;
        if (pngBytes is null || pngBytes.Length == 0) return null;

        try
        {
            var local = capturedAt.LocalDateTime;
            var dayDir = Path.Combine(_baseDir, local.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayDir);

            var baseName = "frame_" + local.ToString("yyyyMMdd_HHmmss_fff");
            if (!string.IsNullOrEmpty(reason))
                baseName += "_" + SanitizeReason(reason);

            var path = Path.Combine(dayDir, baseName + ".png");
            File.WriteAllBytes(path, pngBytes);

            TrimOldest();
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "デバッグフレーム保存に失敗しました。");
            return null;
        }
    }

    private void TrimOldest()
    {
        if (!Directory.Exists(_baseDir)) return;

        var files = Directory.GetFiles(_baseDir, "*.png", SearchOption.AllDirectories);
        if (files.Length <= _maxFiles) return;

        // ファイル名先頭は `frame_yyyyMMdd_HHmmss_fff` なので辞書順 = 時刻順
        var ordered = files
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var deleteCount = ordered.Count - _maxFiles;
        for (int i = 0; i < deleteCount; i++)
        {
            try { File.Delete(ordered[i]); }
            catch (Exception ex) { _logger.LogDebug(ex, "古いフレーム削除に失敗: {Path}", ordered[i]); }
        }
    }

    private static string SanitizeReason(string reason)
    {
        var sb = new StringBuilder(reason.Length);
        foreach (var ch in reason)
            sb.Append(InvalidFileNameChars.Contains(ch) ? '_' : ch);
        return sb.ToString();
    }
}
