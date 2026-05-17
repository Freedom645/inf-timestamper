using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;

namespace InfTimestamper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogging(e.Args);
        Log.Information("INF-TIMESTAMPER starting up");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("INF-TIMESTAMPER shutting down (exit code {ExitCode})", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging(string[] args)
    {
        var minimumLevel = ParseLogLevel(args);
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "app_.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: false,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static LogEventLevel ParseLogLevel(string[] args)
    {
        const string prefix = "--log-level=";
        var raw = args
            .Where(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(a => a[prefix.Length..])
            .FirstOrDefault();

        if (string.IsNullOrEmpty(raw))
            return LogEventLevel.Information;

        return raw.ToLowerInvariant() switch
        {
            "trace" or "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "info" or "information" => LogEventLevel.Information,
            "warn" or "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}
