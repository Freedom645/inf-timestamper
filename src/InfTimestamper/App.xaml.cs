using System.IO;
using System.Windows;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Services;
using InfTimestamper.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace InfTimestamper;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogging(e.Args);
        Log.Information("INF-TIMESTAMPER starting up");

        _host = BuildHost();
        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("INF-TIMESTAMPER shutting down (exit code {ExitCode})", e.ApplicationExitCode);
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Host 停止中に例外が発生しました。");
        }
        finally
        {
            _host?.Dispose();
            Log.CloseAndFlush();
        }
        base.OnExit(e);
    }

    private IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                var settingsPath = SettingsStore.DefaultSettingsPath();
                services.AddSingleton<AppStateMachine>();
                services.AddSingleton<JsonRecordStore>();
                services.AddSingleton<SettingsStore>();
                services.AddSingleton<AppSettings>(sp =>
                {
                    var store = sp.GetRequiredService<SettingsStore>();
                    return store.Load(settingsPath);
                });
                services.AddSingleton<IClipboardService, WpfClipboardService>();
                services.AddSingleton<IDialogService>(_ => new WpfDialogService(() => Current?.MainWindow));
                services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
                    sp.GetRequiredService<AppStateMachine>(),
                    sp.GetRequiredService<IClipboardService>(),
                    sp.GetRequiredService<IDialogService>(),
                    sp.GetRequiredService<JsonRecordStore>(),
                    sp.GetRequiredService<AppSettings>(),
                    sp.GetRequiredService<SettingsStore>(),
                    settingsPath,
                    sp.GetService<ILogger<MainWindowViewModel>>()));
                services.AddSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
            })
            .Build();
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
