using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

public class MainWindowConfirmDialogTests
{
    private static TimestampEntry MakeEntry(string title, DateTimeOffset at)
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = at };
        entry.SetField("title", title);
        return entry;
    }

    private static MainWindowViewModel BuildVm(
        AppSettings settings,
        FakeDialogService dialog,
        SettingsStore? store = null,
        string? settingsPath = null,
        JsonRecordStore? recordStore = null)
    {
        return new MainWindowViewModel(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog,
            recordStore ?? new JsonRecordStore(),
            settings,
            store,
            settingsPath);
    }

    [Fact]
    public void Reset_WithConfirmOn_PromptsBeforeReset()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.ConfirmOnReset = true;
        var dialog = new FakeDialogService { ConfirmResult = false };

        var vm = BuildVm(settings, dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));
        vm.StopCommand.Execute(null);

        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal(1, vm.TimestampCount);

        vm.ResetCommand.Execute(null);

        // Confirm が false → リセットされない
        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal(1, vm.TimestampCount);
    }

    [Fact]
    public void Reset_WithConfirmOff_SkipsConfirmation()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.ConfirmOnReset = false;
        var dialog = new FakeDialogService { ConfirmResult = false };

        var vm = BuildVm(settings, dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        vm.ResetCommand.Execute(null);

        Assert.Equal(AppState.Initial, vm.State);
    }

    [Fact]
    public void RequestExitConfirmation_NoEntriesAndNotRecording_AllowsExit()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.ConfirmOnExit = true;
        var dialog = new FakeDialogService { ConfirmResult = false };

        var vm = BuildVm(settings, dialog);
        Assert.True(vm.RequestExitConfirmation());
    }

    [Fact]
    public void RequestExitConfirmation_Recording_PromptsAndRespectsResult()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.ConfirmOnExit = true;
        var dialog = new FakeDialogService { ConfirmResult = false };

        var vm = BuildVm(settings, dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.False(vm.RequestExitConfirmation());

        dialog.ConfirmResult = true;
        Assert.True(vm.RequestExitConfirmation());
    }

    [Fact]
    public void RequestExitConfirmation_ConfirmDisabled_AlwaysAllowsExit()
    {
        var settings = AppSettings.CreateDefault();
        settings.General.ConfirmOnExit = false;
        var dialog = new FakeDialogService { ConfirmResult = false };

        var vm = BuildVm(settings, dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.True(vm.RequestExitConfirmation());
    }

    [Fact]
    public void CheckUnfinishedRecords_LoadsWhenUserAccepts()
    {
        using var temp = new TempDirectory();
        var settings = AppSettings.CreateDefault();
        settings.General.BackupDirectory = temp.Path;
        var dialog = new FakeDialogService { ConfirmResult = true };
        var store = new JsonRecordStore();

        // 未完了レコード（endedAt = null）を保存
        var record = new StreamRecord
        {
            Game = GameId.Infinitas,
            Stream = new StreamInfo
            {
                StartedAt = new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9)),
                EndedAt = null,
            },
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };
        record.Timestamps.Add(MakeEntry("Sample", record.Stream.StartedAt.AddMinutes(1)));

        var path = Path.Combine(temp.Path,
            JsonRecordStore.GenerateFileName(GameId.Infinitas, record.Stream.StartedAt));
        store.SaveAtomic(record, path);

        var vm = BuildVm(settings, dialog, null, null, store);
        vm.CheckUnfinishedRecords();

        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal(1, vm.TimestampCount);
    }

    [Fact]
    public void CheckUnfinishedRecords_NoUnfinished_DoesNothing()
    {
        using var temp = new TempDirectory();
        var settings = AppSettings.CreateDefault();
        settings.General.BackupDirectory = temp.Path;
        var dialog = new FakeDialogService { ConfirmResult = true };

        var vm = BuildVm(settings, dialog);
        vm.CheckUnfinishedRecords();

        Assert.Equal(AppState.Initial, vm.State);
        Assert.False(dialog.Infos.Any() || dialog.Errors.Any());
    }

    [Fact]
    public void CheckUnfinishedRecords_UserDeclines_DoesNotLoad()
    {
        using var temp = new TempDirectory();
        var settings = AppSettings.CreateDefault();
        settings.General.BackupDirectory = temp.Path;
        var dialog = new FakeDialogService { ConfirmResult = false };
        var store = new JsonRecordStore();

        var record = new StreamRecord
        {
            Game = GameId.Infinitas,
            Stream = new StreamInfo
            {
                StartedAt = new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9)),
                EndedAt = null,
            },
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };
        var path = Path.Combine(temp.Path,
            JsonRecordStore.GenerateFileName(GameId.Infinitas, record.Stream.StartedAt));
        store.SaveAtomic(record, path);

        var vm = BuildVm(settings, dialog, null, null, store);
        vm.CheckUnfinishedRecords();

        Assert.Equal(AppState.Initial, vm.State);
        Assert.Equal(0, vm.TimestampCount);
    }
}
