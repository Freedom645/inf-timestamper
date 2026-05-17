using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.States;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel NewVm(
        FakeClipboardService? clip = null,
        FakeDialogService? dialog = null,
        JsonRecordStore? store = null)
        => new(
            new AppStateMachine(),
            clip ?? new FakeClipboardService(),
            dialog ?? new FakeDialogService(),
            store ?? new JsonRecordStore());

    private static MainWindowViewModel NewVmWith(FakeDialogService dialog)
        => NewVm(null, dialog, null);

    private static TimestampEntry MakeEntry(string title, DateTimeOffset at)
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = at };
        entry.SetField("title", title);
        return entry;
    }

    [Fact]
    public void InitialState_PrimaryButtonIsStart()
    {
        var vm = NewVm();
        Assert.Equal(AppState.Initial, vm.State);
        Assert.Equal("開始", vm.PrimaryButtonText);
        Assert.Same(vm.StartCommand, vm.PrimaryCommand);
        Assert.False(vm.ResetCommand.CanExecute(null));
        Assert.False(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void PrimaryButton_TextAndCommand_FollowStateMachineTransitions()
    {
        var vm = NewVm();

        vm.StartCommand.Execute(null);
        Assert.Equal(AppState.WaitingForStream, vm.State);
        Assert.Equal("強制開始", vm.PrimaryButtonText);
        Assert.Same(vm.ForceStartCommand, vm.PrimaryCommand);

        vm.ForceStartCommand.Execute(null);
        Assert.Equal(AppState.Recording, vm.State);
        Assert.Equal("記録停止", vm.PrimaryButtonText);
        Assert.Same(vm.StopCommand, vm.PrimaryCommand);

        vm.StopCommand.Execute(null);
        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal("記録再開", vm.PrimaryButtonText);
        Assert.Same(vm.ResumeCommand, vm.PrimaryCommand);
    }

    [Fact]
    public void Reset_OnlyAvailableInRecordingEndedState()
    {
        var vm = NewVm();
        Assert.False(vm.ResetCommand.CanExecute(null));

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        Assert.True(vm.ResetCommand.CanExecute(null));
        vm.ResetCommand.Execute(null);
        Assert.Equal(AppState.Initial, vm.State);
        Assert.Equal(0, vm.TimestampCount);
    }

    [Fact]
    public void ForceStart_SetsStreamStartedAt()
    {
        var vm = NewVm();
        Assert.Null(vm.StreamStartedAt);
        Assert.Equal("-", vm.StreamStartedAtText);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.NotNull(vm.StreamStartedAt);
        Assert.NotEqual("-", vm.StreamStartedAtText);
    }

    [Fact]
    public void AddTimestamp_IncrementsCountAndEnablesCopy()
    {
        var vm = NewVm();
        Assert.False(vm.CopyCommand.CanExecute(null));

        vm.AddTimestamp(MakeEntry("Hello", DateTimeOffset.Now));

        Assert.Equal(1, vm.TimestampCount);
        Assert.True(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void Copy_WritesAllTimestampsToClipboard()
    {
        var clip = new FakeClipboardService();
        var vm = NewVm(clip);
        vm.Format = "$title";

        vm.AddTimestamp(MakeEntry("First", DateTimeOffset.UnixEpoch));
        vm.AddTimestamp(MakeEntry("Second", DateTimeOffset.UnixEpoch.AddSeconds(60)));

        vm.CopyCommand.Execute(null);

        Assert.Equal($"First{Environment.NewLine}Second{Environment.NewLine}", clip.LastText);
    }

    [Fact]
    public void FormatChange_UpdatesExistingTimestampDisplayTexts()
    {
        var vm = NewVm();
        vm.Format = "$title";
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));

        Assert.Equal("X", vm.Timestamps[0].DisplayText);
        vm.Format = "[$title]";
        Assert.Equal("[X]", vm.Timestamps[0].DisplayText);
    }

    [Fact]
    public void AddTimestamp_InsertsInPlayStartedAtOrder()
    {
        var vm = NewVm();
        var earlier = MakeEntry("E", DateTimeOffset.UnixEpoch.AddSeconds(100));
        var later = MakeEntry("L", DateTimeOffset.UnixEpoch.AddSeconds(200));

        vm.AddTimestamp(later);
        vm.AddTimestamp(earlier);

        Assert.Same(earlier, vm.Timestamps[0].Entry);
        Assert.Same(later, vm.Timestamps[1].Entry);
    }

    [Fact]
    public void StateLabel_ProjectsAllStates()
    {
        var vm = NewVm();
        Assert.Equal("初期状態", vm.StateLabel);

        vm.StartCommand.Execute(null);
        Assert.Equal("配信開始待ち", vm.StateLabel);

        vm.ForceStartCommand.Execute(null);
        Assert.Equal("記録中", vm.StateLabel);

        vm.StopCommand.Execute(null);
        Assert.Equal("記録終了", vm.StateLabel);
    }

    [Fact]
    public void EditStreamStartedAt_AppliesNewValue()
    {
        var dialog = new FakeDialogService();
        var vm = NewVmWith(dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        var newValue = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.FromHours(9));
        dialog.DateTimeEditorResult = new[] { newValue };

        vm.EditStreamStartedAtCommand.Execute(null);

        Assert.Equal(newValue, vm.StreamStartedAt);
    }

    [Fact]
    public void EditStreamStartedAt_DisabledWhenNoStreamStart()
    {
        var vm = NewVm();
        Assert.False(vm.EditStreamStartedAtCommand.CanExecute(null));
    }

    [Fact]
    public void EditSelectedTimestamps_AppliesShift_AndReorders()
    {
        var dialog = new FakeDialogService();
        var vm = NewVmWith(dialog);
        vm.SetStreamStartedAt(DateTimeOffset.UnixEpoch);

        var first = MakeEntry("First", DateTimeOffset.UnixEpoch.AddSeconds(100));
        var second = MakeEntry("Second", DateTimeOffset.UnixEpoch.AddSeconds(200));
        vm.AddTimestamp(first);
        vm.AddTimestamp(second);

        vm.Timestamps[1].IsSelected = true;
        vm.NotifySelectionChanged();

        // 選択中の値 (second の 200 秒) を 50 秒に変更 → first(100s) より前に並ぶ
        dialog.DateTimeEditorResult = new[] { DateTimeOffset.UnixEpoch.AddSeconds(50) };
        vm.EditSelectedTimestampsCommand.Execute(null);

        Assert.Same(second, vm.Timestamps[0].Entry);
        Assert.Same(first, vm.Timestamps[1].Entry);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(50), second.PlayStartedAt);
    }

    [Fact]
    public void EditSelectedTimestamps_DisabledWithoutSelection()
    {
        var vm = NewVm();
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));
        Assert.False(vm.EditSelectedTimestampsCommand.CanExecute(null));
    }

    [Fact]
    public void OpenRecord_LoadsFromFile_AndTransitionsToRecordingEnded()
    {
        var dialog = new FakeDialogService();
        var store = new JsonRecordStore();
        var vm = new MainWindowViewModel(new AppStateMachine(), new FakeClipboardService(), dialog, store);

        // 事前に保存しておく
        var record = new StreamRecord
        {
            Game = GameId.Infinitas,
            Stream = new StreamInfo { StartedAt = new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9)) },
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };
        var entry = MakeEntry("Sample", record.Stream.StartedAt.AddMinutes(5));
        record.Timestamps.Add(entry);

        var tempPath = Path.Combine(Path.GetTempPath(), $"inf-test-{Guid.NewGuid():N}.json");
        try
        {
            store.SaveAtomic(record, tempPath);

            dialog.OpenFileResult = tempPath;
            vm.OpenRecordCommand.Execute(null);

            Assert.Equal(AppState.RecordingEnded, vm.State);
            Assert.Equal(1, vm.TimestampCount);
            Assert.Equal(record.Stream.StartedAt, vm.StreamStartedAt);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void OpenRecord_BrokenFile_ShowsErrorDialog()
    {
        var dialog = new FakeDialogService();
        var vm = NewVmWith(dialog);

        var tempPath = Path.Combine(Path.GetTempPath(), $"inf-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, "{ broken json");
            dialog.OpenFileResult = tempPath;
            vm.OpenRecordCommand.Execute(null);

            Assert.Single(dialog.Errors);
            Assert.Equal(AppState.Initial, vm.State);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void OpenRecord_CancelDialog_DoesNothing()
    {
        var dialog = new FakeDialogService { OpenFileResult = null };
        var vm = NewVmWith(dialog);

        vm.OpenRecordCommand.Execute(null);

        Assert.Empty(dialog.Errors);
        Assert.Equal(AppState.Initial, vm.State);
    }

    [Fact]
    public void SaveRecord_WritesFile_AndShowsInfo()
    {
        var dialog = new FakeDialogService();
        var vm = NewVmWith(dialog);
        vm.SetStreamStartedAt(new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9)));
        vm.AddTimestamp(MakeEntry("Sample", vm.StreamStartedAt!.Value.AddMinutes(1)));

        var tempPath = Path.Combine(Path.GetTempPath(), $"inf-test-{Guid.NewGuid():N}.json");
        try
        {
            dialog.SaveFileResult = tempPath;
            vm.SaveRecordCommand.Execute(null);

            Assert.True(File.Exists(tempPath));
            Assert.Single(dialog.Infos);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveRecord_DisabledWhenEmpty()
    {
        var vm = NewVm();
        Assert.False(vm.SaveRecordCommand.CanExecute(null));
    }
}
