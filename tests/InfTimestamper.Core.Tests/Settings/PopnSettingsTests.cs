using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Settings;

public class PopnSettingsTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsPopnSection()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        var store = new SettingsStore();

        var settings = AppSettings.CreateDefault();
        settings.Popn.TimestampFormat = "$timestamp $title ($rank, $medal)";
        settings.Popn.TrackerDirectory = @"C:\popn\live";
        settings.General.SelectedGame = GameIdExtensions.PopnSerialized;

        store.SaveAtomic(settings, path);
        var loaded = store.Load(path);

        Assert.Equal("$timestamp $title ($rank, $medal)", loaded.Popn.TimestampFormat);
        Assert.Equal(@"C:\popn\live", loaded.Popn.TrackerDirectory);
        Assert.Equal(GameId.Popn, loaded.ResolveSelectedGame());
    }

    [Fact]
    public void Load_LegacyFileWithoutPopnSection_FallsBackToDefaults()
    {
        // Phase 8 までの settings.json（popn / selectedGame が無い）を読んでも壊れないこと
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "general": { "autoUpdateCheck": true, "backupDirectory": "C:\\backups" },
              "obs": { "host": "127.0.0.1", "port": 4455, "password": "" },
              "infinitas": { "timestampFormat": "$timestamp $title", "refluxDirectory": "C:\\reflux" }
            }
            """);

        var loaded = new SettingsStore().Load(path);

        Assert.Equal(GameId.Infinitas, loaded.ResolveSelectedGame());
        Assert.NotNull(loaded.Popn);
        Assert.Equal(string.Empty, loaded.WatchTargetFor(GameId.Popn));
        Assert.Equal(AppSettings.DefaultTimestampFormat, loaded.TimestampFormatFor(GameId.Popn));
        Assert.Equal(@"C:\reflux", loaded.WatchTargetFor(GameId.Infinitas));
    }

    [Fact]
    public void TimestampFormatFor_ReturnsPerGameFormat()
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.TimestampFormat = "$timestamp $title [$diff_s]";
        settings.Popn.TimestampFormat = "$timestamp $title ($rank)";

        Assert.Equal("$timestamp $title [$diff_s]", settings.TimestampFormatFor(GameId.Infinitas));
        Assert.Equal("$timestamp $title ($rank)", settings.TimestampFormatFor(GameId.Popn));
    }

    [Fact]
    public void TimestampFormatFor_EmptyFormat_FallsBackToDefault()
    {
        var settings = AppSettings.CreateDefault();
        settings.Popn.TimestampFormat = string.Empty;

        Assert.Equal(AppSettings.DefaultTimestampFormat, settings.TimestampFormatFor(GameId.Popn));
    }

    [Fact]
    public void WatchTargetFor_ReturnsPerGameDirectory()
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.RefluxDirectory = @"C:\reflux";
        settings.Popn.TrackerDirectory = @"C:\popn";

        Assert.Equal(@"C:\reflux", settings.WatchTargetFor(GameId.Infinitas));
        Assert.Equal(@"C:\popn", settings.WatchTargetFor(GameId.Popn));
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveSelectedGame_UnknownValue_FallsBackToInfinitas(string? raw)
    {
        var settings = AppSettings.CreateDefault();
        settings.General.SelectedGame = raw!;

        Assert.Equal(GameId.Infinitas, settings.ResolveSelectedGame());
    }
}
