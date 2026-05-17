using InfTimestamper.Core.Settings;

namespace InfTimestamper.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void CreateDefault_PopulatesAllSubObjects()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.NotNull(settings.General);
        Assert.NotNull(settings.Obs);
        Assert.NotNull(settings.Infinitas);
        Assert.NotNull(settings.Infinitas.CaptureObs);

        Assert.Equal(AppSettings.DefaultObsHost, settings.Obs.Host);
        Assert.Equal(AppSettings.DefaultObsPort, settings.Obs.Port);
        Assert.Equal(AppSettings.DefaultTimestampFormat, settings.Infinitas.TimestampFormat);
        Assert.True(settings.General.AutoUpdateCheck);
        Assert.True(settings.General.ConfirmOnReset);
        Assert.True(settings.General.ConfirmOnExit);
    }

    [Fact]
    public void DefaultBackupDirectory_UsesAppDataRoaming()
    {
        var dir = AppSettings.DefaultBackupDirectory();
        Assert.Contains("inf-timestamper", dir);
        Assert.Contains("backups", dir);
    }
}
