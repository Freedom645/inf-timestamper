using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Settings;

public class YouTubeSettingsTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsYouTubeSection()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        var store = new SettingsStore();

        var settings = AppSettings.CreateDefault();
        settings.YouTube.Enabled = true;
        settings.YouTube.DescriptionHeading = "【セトリ】";
        settings.YouTube.UpdateIntervalSeconds = 120;

        store.SaveAtomic(settings, path);
        var loaded = store.Load(path);

        Assert.True(loaded.YouTube.Enabled);
        Assert.Equal("【セトリ】", loaded.YouTube.DescriptionHeading);
        Assert.Equal(120, loaded.YouTube.UpdateIntervalSeconds);
    }

    [Fact]
    public void Load_FileWithoutYouTubeSection_FallsBackToDefaults()
    {
        // v1.2.0 までの settings.json（youTube セクションが無い）を読んでも壊れないこと
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "general": { "autoUpdateCheck": true, "backupDirectory": "C:\\backups" },
              "youTube": null
            }
            """);

        var loaded = new SettingsStore().Load(path);

        Assert.NotNull(loaded.YouTube);
        Assert.False(loaded.YouTube.Enabled);
        Assert.Equal(AppSettings.DefaultYouTubeDescriptionHeading, loaded.YouTube.ResolveHeading());
        Assert.Equal(TimeSpan.FromSeconds(AppSettings.DefaultYouTubeUpdateIntervalSeconds), loaded.YouTube.ResolveUpdateInterval());
    }

    [Fact]
    public void ResolveUpdateInterval_ClampsToMinimum()
    {
        var settings = new YouTubeSettings { UpdateIntervalSeconds = 5 };

        Assert.Equal(TimeSpan.FromSeconds(AppSettings.MinYouTubeUpdateIntervalSeconds), settings.ResolveUpdateInterval());
    }

    [Fact]
    public void SettingsFile_DoesNotContainCredentials()
    {
        // ログイン情報は DPAPI で暗号化した別ファイルに置き、settings.json には書かない
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        new SettingsStore().SaveAtomic(AppSettings.CreateDefault(), path);

        var json = File.ReadAllText(path);

        Assert.Contains("\"youTube\"", json);
        Assert.DoesNotContain("clientSecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
    }
}
