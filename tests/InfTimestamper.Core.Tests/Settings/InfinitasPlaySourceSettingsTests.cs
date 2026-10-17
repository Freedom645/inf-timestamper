using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Settings;

/// <summary>INFINITAS の検知元の選択（Reflux / 打鍵カウンタ。issue #13）。</summary>
public class InfinitasPlaySourceSettingsTests
{
    [Fact]
    public void Default_UsesReflux()
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.RefluxDirectory = @"C:\reflux";

        Assert.Equal(PlaySourceId.Reflux, settings.PlaySourceFor(GameId.Infinitas));
        Assert.Equal(WatchTarget.ForDirectory(@"C:\reflux"), settings.WatchTargetFor(GameId.Infinitas));
    }

    [Fact]
    public void DakenCounter_WatchesEndpointInsteadOfRefluxDirectory()
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.RefluxDirectory = @"C:\reflux";
        settings.Infinitas.PlaySource = PlaySourceIdExtensions.DakenCounterSerialized;
        settings.Infinitas.DakenCounterHost = "192.168.1.10";
        settings.Infinitas.DakenCounterPort = 9000;

        Assert.Equal(PlaySourceId.DakenCounter, settings.PlaySourceFor(GameId.Infinitas));
        Assert.Equal(WatchTarget.ForEndpoint("ws://192.168.1.10:9000"), settings.WatchTargetFor(GameId.Infinitas));
    }

    [Fact]
    public void DakenCounter_BlankHostAndPort_FallBackToDefaults()
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.PlaySource = PlaySourceIdExtensions.DakenCounterSerialized;
        settings.Infinitas.DakenCounterHost = " ";
        settings.Infinitas.DakenCounterPort = 0;

        Assert.Equal("ws://127.0.0.1:8767", settings.WatchTargetFor(GameId.Infinitas).Endpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("REFLUX")]
    public void UnknownPlaySource_FallsBackToReflux(string raw)
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.PlaySource = raw;

        Assert.Equal(PlaySourceId.Reflux, settings.PlaySourceFor(GameId.Infinitas));
    }

    [Theory]
    [InlineData(GameId.Popn, PlaySourceId.PopnTracker)]
    [InlineData(GameId.Sdvx, PlaySourceId.SdvxHelper)]
    public void OtherGames_AreNotAffectedByInfinitasSelection(GameId game, PlaySourceId expected)
    {
        var settings = AppSettings.CreateDefault();
        settings.Infinitas.PlaySource = PlaySourceIdExtensions.DakenCounterSerialized;

        Assert.Equal(expected, settings.PlaySourceFor(game));
    }

    [Fact]
    public void SaveAndLoad_RoundTripsPlaySource()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        var store = new SettingsStore();

        var settings = AppSettings.CreateDefault();
        settings.Infinitas.PlaySource = PlaySourceIdExtensions.DakenCounterSerialized;
        settings.Infinitas.DakenCounterHost = "192.168.1.10";
        settings.Infinitas.DakenCounterPort = 9000;

        store.SaveAtomic(settings, path);
        var loaded = store.Load(path);

        Assert.Equal(PlaySourceIdExtensions.DakenCounterSerialized, loaded.Infinitas.PlaySource);
        Assert.Equal("192.168.1.10", loaded.Infinitas.DakenCounterHost);
        Assert.Equal(9000, loaded.Infinitas.DakenCounterPort);
        Assert.Equal(PlaySourceId.DakenCounter, loaded.PlaySourceFor(GameId.Infinitas));
    }

    [Fact]
    public void Load_FileWithoutPlaySource_KeepsUsingReflux()
    {
        // v1.2.0 までの settings.json（検知元の選択が無い）を読んでも従来どおり Reflux で動くこと
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "infinitas": { "timestampFormat": "$timestamp $title", "refluxDirectory": "C:\\reflux" }
            }
            """);

        var loaded = new SettingsStore().Load(path);

        Assert.Equal(PlaySourceId.Reflux, loaded.PlaySourceFor(GameId.Infinitas));
        Assert.Equal(@"C:\reflux", loaded.WatchTargetFor(GameId.Infinitas).Directory);
        Assert.Equal(AppSettings.DefaultDakenCounterHost, loaded.Infinitas.DakenCounterHost);
        Assert.Equal(AppSettings.DefaultDakenCounterPort, loaded.Infinitas.DakenCounterPort);
    }
}
