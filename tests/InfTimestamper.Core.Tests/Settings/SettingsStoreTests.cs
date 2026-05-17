using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Settings;

public class SettingsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore();
        var path = Path.Combine(temp.Path, "settings.json");

        var settings = store.Load(path);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(AppSettings.DefaultTimestampFormat, settings.Infinitas.TimestampFormat);
    }

    [Fact]
    public void Load_BrokenFile_ReturnsDefault()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{ broken");

        var settings = store.Load(path);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void SaveAtomic_ThenLoad_RoundTrip()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore();
        var path = Path.Combine(temp.Path, "settings.json");

        var settings = AppSettings.CreateDefault();
        settings.Obs.Host = "192.168.1.10";
        settings.Obs.Port = 4567;
        settings.Obs.Password = "secret";
        settings.Infinitas.TimestampFormat = "[$timestamp] $title";
        settings.Infinitas.GameSourceName = "INF";
        settings.Infinitas.TwoPcEnabled = true;
        settings.General.AutoUpdateCheck = false;

        store.SaveAtomic(settings, path);
        Assert.True(File.Exists(path));

        var loaded = store.Load(path);
        Assert.Equal("192.168.1.10", loaded.Obs.Host);
        Assert.Equal(4567, loaded.Obs.Port);
        Assert.Equal("secret", loaded.Obs.Password);
        Assert.Equal("[$timestamp] $title", loaded.Infinitas.TimestampFormat);
        Assert.Equal("INF", loaded.Infinitas.GameSourceName);
        Assert.True(loaded.Infinitas.TwoPcEnabled);
        Assert.False(loaded.General.AutoUpdateCheck);
    }

    [Fact]
    public void SaveAtomic_DoesNotLeaveTempOrBakFiles()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore();
        var path = Path.Combine(temp.Path, "settings.json");

        store.SaveAtomic(AppSettings.CreateDefault(), path);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void SaveAtomic_OverwritesExistingFile()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore();
        var path = Path.Combine(temp.Path, "settings.json");

        var first = AppSettings.CreateDefault();
        first.Obs.Port = 1111;
        store.SaveAtomic(first, path);

        var second = AppSettings.CreateDefault();
        second.Obs.Port = 2222;
        store.SaveAtomic(second, path);

        var loaded = store.Load(path);
        Assert.Equal(2222, loaded.Obs.Port);
    }
}
