using Microsoft.Extensions.Logging;

namespace InfTimestamper.Core.Tests.Obs;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    public int CountAt(LogLevel level)
    {
        lock (_gate)
        {
            int count = 0;
            foreach (var e in _entries)
                if (e.Level == level) count++;
            return count;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception);
