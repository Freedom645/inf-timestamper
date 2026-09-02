using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Settings;

public class SdvxSettingsTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsSdvxSection()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        var store = new SettingsStore();

        var settings = AppSettings.CreateDefault();
        settings.Sdvx.TimestampFormat = "$timestamp $title [$diff_s $level] $score $clear_lamp";
        settings.Sdvx.HelperHost = "192.168.1.20";
        settings.Sdvx.HelperPort = 9100;
        settings.General.SelectedGame = GameIdExtensions.SdvxSerialized;

        store.SaveAtomic(settings, path);
        var loaded = store.Load(path);

        Assert.Equal("$timestamp $title [$diff_s $level] $score $clear_lamp", loaded.Sdvx.TimestampFormat);
        Assert.Equal("192.168.1.20", loaded.Sdvx.HelperHost);
        Assert.Equal(9100, loaded.Sdvx.HelperPort);
        Assert.Equal(GameId.Sdvx, loaded.ResolveSelectedGame());
    }

    [Fact]
    public void Load_FileWithoutSdvxSection_FallsBackToDefaults()
    {
        // Phase 10 までの settings.json（sdvx セクションが無い）を読んでも壊れないこと
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "general": { "autoUpdateCheck": true, "backupDirectory": "C:\\backups" },
              "obs": { "host": "127.0.0.1", "port": 4455, "password": "" },
              "infinitas": { "timestampFormat": "$timestamp $title", "refluxDirectory": "C:\\reflux" },
              "popn": { "timestampFormat": "$timestamp $title", "trackerDirectory": "C:\\popn" }
            }
            """);

        var loaded = new SettingsStore().Load(path);

        Assert.NotNull(loaded.Sdvx);
        Assert.Equal(AppSettings.DefaultSdvxHelperHost, loaded.Sdvx.HelperHost);
        Assert.Equal(AppSettings.DefaultSdvxHelperPort, loaded.Sdvx.HelperPort);
        Assert.Equal(AppSettings.DefaultTimestampFormat, loaded.TimestampFormatFor(GameId.Sdvx));
    }

    [Fact]
    public void WatchTargetFor_Sdvx_IsTheHelperWebSocketEndpoint()
    {
        var settings = AppSettings.CreateDefault();
        settings.Sdvx.HelperPort = 8767;

        Assert.Equal("ws://127.0.0.1:8767", settings.WatchTargetFor(GameId.Sdvx));

        settings.Sdvx.HelperPort = 9100;
        Assert.Equal("ws://127.0.0.1:9100", settings.WatchTargetFor(GameId.Sdvx));

        settings.Sdvx.HelperHost = "192.168.1.20";
        Assert.Equal("ws://192.168.1.20:9100", settings.WatchTargetFor(GameId.Sdvx));
    }

    [Fact]
    public void SdvxHelperEndpoint_EmptyHost_FallsBackToTheDefault()
    {
        Assert.Equal("ws://127.0.0.1:8767", AppSettings.SdvxHelperEndpoint(null, 8767));
        Assert.Equal("ws://127.0.0.1:8767", AppSettings.SdvxHelperEndpoint("  ", 8767));
    }

    [Fact]
    public void TimestampFormatFor_ReturnsTheSdvxFormat()
    {
        var settings = AppSettings.CreateDefault();
        settings.Sdvx.TimestampFormat = "$timestamp $title ($grade)";

        Assert.Equal("$timestamp $title ($grade)", settings.TimestampFormatFor(GameId.Sdvx));
    }
}
