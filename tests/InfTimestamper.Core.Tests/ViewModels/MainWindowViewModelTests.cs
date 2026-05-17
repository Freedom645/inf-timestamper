using InfTimestamper.Core.Models;
using InfTimestamper.Core.States;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel NewVm(FakeClipboardService? clip = null)
        => new(new AppStateMachine(), clip ?? new FakeClipboardService());

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
}
