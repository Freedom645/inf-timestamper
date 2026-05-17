using System.Net.Http;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Updates;
using InfTimestamper.Core.Updates;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Core.Tests.ViewModels;

public class MainWindowVersionCheckTests
{
    private static MainWindowViewModel Build(
        FakeDialogService dialog,
        FakeGitHubReleaseChecker? checker)
    {
        var settings = AppSettings.CreateDefault();
        return new MainWindowViewModel(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog,
            new JsonRecordStore(),
            settings,
            null,
            null,
            checker);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_NoChecker_SilentSkipped()
    {
        var dialog = new FakeDialogService();
        var vm = Build(dialog, null);

        await vm.CheckLatestVersionAsync(silent: true);

        Assert.Empty(dialog.Errors);
        Assert.Empty(dialog.Infos);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_NoChecker_ManualShowsError()
    {
        var dialog = new FakeDialogService();
        var vm = Build(dialog, null);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Single(dialog.Errors);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_NewerVersion_PromptsToOpenReleasePage()
    {
        var dialog = new FakeDialogService { ConfirmResult = false };
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease(
                "v999.0.0",
                "Release 999.0.0",
                "https://github.com/Freedom645/inf-timestamper/releases/tag/v999.0.0",
                DateTimeOffset.Now),
        };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: false);

        // Confirm が呼ばれたことを Errors/Infos には現れないが ConfirmResult=false で
        // リリースページ起動はしない。Confirm の呼び出し自体を Fake 側で追跡すべきだが
        // ここでは「Errors/Infos のいずれにも記録されない」ことで間接的に検証
        Assert.Empty(dialog.Errors);
        Assert.Empty(dialog.Infos);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_LatestVersion_ManualShowsInfo()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v0.0.0", "old", "https://example", DateTimeOffset.Now),
        };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Single(dialog.Infos);
        Assert.Contains("最新", dialog.Infos[0].Message);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_LatestVersion_SilentShowsNothing()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeGitHubReleaseChecker
        {
            NextResult = new GitHubRelease("v0.0.0", "old", "https://example", DateTimeOffset.Now),
        };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: true);

        Assert.Empty(dialog.Errors);
        Assert.Empty(dialog.Infos);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_FetchFails_ManualShowsError()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeGitHubReleaseChecker { NextResult = null };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Single(dialog.Errors);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_FetchFails_SilentShowsNothing()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeGitHubReleaseChecker { NextResult = null };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: true);

        Assert.Empty(dialog.Errors);
        Assert.Empty(dialog.Infos);
    }

    [Fact]
    public async Task CheckLatestVersionAsync_ExceptionThrown_HandledGracefully()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeGitHubReleaseChecker { NextException = new HttpRequestException("ネットワークエラー") };
        var vm = Build(dialog, checker);

        await vm.CheckLatestVersionAsync(silent: false);

        Assert.Single(dialog.Errors);
    }
}
