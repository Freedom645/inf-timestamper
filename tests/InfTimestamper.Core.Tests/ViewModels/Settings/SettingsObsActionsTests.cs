using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.Obs;
using InfTimestamper.Core.Tests.ViewModels;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class SettingsObsActionsTests
{
    [Fact]
    public async Task ObsSettings_TestConnection_Success_ShowsInfoDialog()
    {
        var dialog = new FakeDialogService();
        var conn = new FakeObsConnection
        {
            ServerInfoHandler = () => Task.FromResult(new ObsServerInfo("31.0.0", "INFINITAS")),
        };
        var tester = new ObsConnectionTester(() => conn);
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings(), tester, dialog);

        vm.TestConnectionCommand.Execute(null);
        await WaitUntilAsync(() => dialog.Infos.Count > 0 || dialog.Errors.Count > 0, 1000);

        Assert.Single(dialog.Infos);
        Assert.Contains("31.0.0", dialog.Infos[0].Message);
        Assert.Contains("INFINITAS", dialog.Infos[0].Message);
    }

    [Fact]
    public async Task ObsSettings_TestConnection_Failure_ShowsErrorDialog()
    {
        var dialog = new FakeDialogService();
        var conn = new FakeObsConnection
        {
            ConnectHandler = _ => Task.FromException(new InvalidOperationException("Connect refused")),
        };
        var tester = new ObsConnectionTester(() => conn);
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings(), tester, dialog);

        vm.TestConnectionCommand.Execute(null);
        await WaitUntilAsync(() => dialog.Errors.Count > 0, 1000);

        Assert.Single(dialog.Errors);
        Assert.Contains("Connect refused", dialog.Errors[0].Message);
    }

    [Fact]
    public void GeneralSettings_BrowseBackupDirectory_AppliesSelectedFolder()
    {
        var dialog = new FakeDialogService { FolderBrowserResult = @"C:\custom\backups" };
        var vm = new GeneralSettingsViewModel(
            new GeneralSettings { BackupDirectory = "old" },
            dialog);

        vm.BrowseBackupDirectoryCommand.Execute(null);

        Assert.Equal(@"C:\custom\backups", vm.BackupDirectory);
    }

    [Fact]
    public void GeneralSettings_BrowseBackupDirectory_Cancelled_KeepsOriginal()
    {
        var dialog = new FakeDialogService { FolderBrowserResult = null };
        var vm = new GeneralSettingsViewModel(
            new GeneralSettings { BackupDirectory = "old" },
            dialog);

        vm.BrowseBackupDirectoryCommand.Execute(null);

        Assert.Equal("old", vm.BackupDirectory);
    }

    private static async Task WaitUntilAsync(Func<bool> cond, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (cond()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("条件が成立しませんでした。");
    }
}
