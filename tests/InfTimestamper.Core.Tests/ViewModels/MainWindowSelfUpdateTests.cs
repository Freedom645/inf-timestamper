using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Updates;
using InfTimestamper.Core.Updates;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Core.Tests.ViewModels;

public class MainWindowSelfUpdateTests
{
    private static MainWindowViewModel Build(
        FakeDialogService dialog,
        FakeGitHubReleaseChecker checker,
        FakeUpdateService updateService)
    {
        return new MainWindowViewModel(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog,
            new JsonRecordStore(),
            AppSettings.CreateDefault(),
            null,
            null,
            checker,
            updateService);
    }

    [Fact]
    public async Task CheckLatestVersion_VelopackInstalled_NewerVersion_TriggersSelfUpdate()
    {
        var dialog = new FakeDialogService
        {
            ConfirmResult = true,
            UpdateProgressResult = true,
        };
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v999.0.0", "Release", "https://example", DateTimeOffset.Now),
        };
        var updateService = new FakeUpdateService
        {
            IsInstalled = true,
            DownloadResult = true,
        };
        var vm = Build(dialog, checker, updateService);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Equal(1, dialog.UpdateProgressCallCount);
        Assert.Equal(1, updateService.ApplyAndRestartCallCount);
    }

    [Fact]
    public async Task CheckLatestVersion_VelopackInstalled_UserDeclines_DoesNotUpdate()
    {
        var dialog = new FakeDialogService { ConfirmResult = false };
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v999.0.0", "Release", "https://example", DateTimeOffset.Now),
        };
        var updateService = new FakeUpdateService { IsInstalled = true };
        var vm = Build(dialog, checker, updateService);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Equal(0, dialog.UpdateProgressCallCount);
        Assert.Equal(0, updateService.ApplyAndRestartCallCount);
    }

    [Fact]
    public async Task CheckLatestVersion_VelopackInstalled_DownloadFails_ShowsError()
    {
        var dialog = new FakeDialogService
        {
            ConfirmResult = true,
            UpdateProgressResult = false,
        };
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v999.0.0", "Release", "https://example", DateTimeOffset.Now),
        };
        var updateService = new FakeUpdateService
        {
            IsInstalled = true,
            DownloadResult = false,
        };
        var vm = Build(dialog, checker, updateService);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Equal(1, dialog.UpdateProgressCallCount);
        Assert.Equal(0, updateService.ApplyAndRestartCallCount);
        Assert.Single(dialog.Errors);
    }

    [Fact]
    public async Task CheckLatestVersion_VelopackNotInstalled_FallsBackToReleasePage()
    {
        var dialog = new FakeDialogService { ConfirmResult = false };
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v999.0.0", "Release", "https://example", DateTimeOffset.Now),
        };
        var updateService = new FakeUpdateService { IsInstalled = false };
        var vm = Build(dialog, checker, updateService);

        await vm.CheckLatestVersionAsync(silent: false);

        // Velopack 未インストール → ShowUpdateProgressAsync は呼ばれない
        Assert.Equal(0, dialog.UpdateProgressCallCount);
        Assert.Equal(0, updateService.ApplyAndRestartCallCount);
    }
}
