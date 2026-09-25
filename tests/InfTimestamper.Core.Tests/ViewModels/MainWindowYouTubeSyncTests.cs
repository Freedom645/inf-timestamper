using System.Diagnostics;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.Core.YouTube;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>記録の状態遷移と YouTube の概要欄の同期の結線。</summary>
public class MainWindowYouTubeSyncTests
{
    private static MainWindowViewModel BuildVm(AppStateMachine sm, AppSettings settings, YouTubeDescriptionSync sync)
        => new(
            sm,
            new FakeClipboardService(),
            new FakeDialogService(),
            new JsonRecordStore(),
            settings,
            null,
            null,
            null,
            null,
            logger: null,
            fileRecycler: null,
            youTubeSync: sync);

    private static AppSettings EnabledSettings()
    {
        var settings = TestSettingsFactory.CreateDefault();
        settings.YouTube.Enabled = true;
        settings.Infinitas.TimestampFormat = "$timestamp $title";
        return settings;
    }

    private static TimestampEntry MakeEntry(string title, DateTimeOffset at)
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = at };
        entry.SetField("title", title);
        return entry;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("条件が満たされませんでした。");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Recording_WritesTimestampsAndFinalOnStop()
    {
        var api = new FakeYouTubeApi("説明文", DateTimeOffset.Now);
        await using var sync = new YouTubeDescriptionSync(api);
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, EnabledSettings(), sync);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        Assert.True(sync.IsActive);

        vm.AddTimestamp(MakeEntry("曲A", vm.StreamStartedAt!.Value.AddMinutes(5)));
        await WaitUntilAsync(() => api.Updates.Count == 1);
        Assert.Equal("説明文\n\n▼タイムスタンプ\n00:00:00 配信開始\n00:05:00 曲A", api.Description);

        // 間隔（60 秒）の内側でも、記録停止では最終版を書き込む
        vm.AddTimestamp(MakeEntry("曲B", vm.StreamStartedAt!.Value.AddMinutes(9)));
        vm.StopCommand.Execute(null);

        await WaitUntilAsync(() => api.Updates.Count == 2);
        Assert.Contains("曲B", api.Description);
        await WaitUntilAsync(() => !sync.IsActive);
    }

    [Fact]
    public async Task NoPlaysYet_DoesNotWrite()
    {
        // 「配信開始」行だけのブロックは書かない（API の消費を抑える）
        var api = new FakeYouTubeApi("説明文", DateTimeOffset.Now);
        await using var sync = new YouTubeDescriptionSync(api);
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, EnabledSettings(), sync);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.StopCommand.Execute(null);
        await WaitUntilAsync(() => !sync.IsActive);

        Assert.Empty(api.Updates);
    }

    [Fact]
    public async Task Disabled_DoesNotStartSync()
    {
        var api = new FakeYouTubeApi("説明文", DateTimeOffset.Now);
        await using var sync = new YouTubeDescriptionSync(api);
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault(), sync);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.False(sync.IsActive);
        Assert.False(vm.IsYouTubeStatusVisible);
        Assert.Equal(string.Empty, vm.YouTubeStatusText);
    }

    [Fact]
    public async Task StatusText_ReflectsSyncState()
    {
        var api = new FakeYouTubeApi("説明文", DateTimeOffset.Now);
        await using var sync = new YouTubeDescriptionSync(api);
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, EnabledSettings(), sync);
        Assert.Equal("待機中", vm.YouTubeStatusText);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        await WaitUntilAsync(() => sync.Status.State == YouTubeSyncState.Linked);
        Assert.Equal("連携中（テスト配信）", vm.YouTubeStatusText);
    }
}
