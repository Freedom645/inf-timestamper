using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.States;
using InfTimestamper.ViewModels;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>
/// 記録操作ボタンの隣のボタン（リセット / 停止）の状態別の振る舞い。
/// `配信開始待ち` では記録操作ボタンが「強制開始」になるため、待機をやめる導線をここに置いている。
/// </summary>
public class MainWindowSecondaryButtonTests
{
    private static MainWindowViewModel NewVm(FakeDialogService? dialog = null)
        => new(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog ?? new FakeDialogService(),
            new JsonRecordStore());

    [Fact]
    public void InitialState_ShowsResetAndIsDisabled()
    {
        var vm = NewVm();

        Assert.Equal("リセット", vm.SecondaryButtonText);
        Assert.Same(vm.ResetCommand, vm.SecondaryCommand);
        Assert.False(vm.SecondaryCommand.CanExecute(null));
    }

    [Fact]
    public void WaitingForStream_ShowsStopAndIsEnabled()
    {
        var vm = NewVm();
        vm.StartCommand.Execute(null);

        Assert.Equal(AppState.WaitingForStream, vm.State);
        Assert.Equal("停止", vm.SecondaryButtonText);
        Assert.Same(vm.StopCommand, vm.SecondaryCommand);
        Assert.True(vm.SecondaryCommand.CanExecute(null));
    }

    [Fact]
    public void WaitingForStream_Stop_ReturnsToInitial()
    {
        var vm = NewVm();
        vm.StartCommand.Execute(null);

        vm.SecondaryCommand.Execute(null);

        Assert.Equal(AppState.Initial, vm.State);
        Assert.Equal("リセット", vm.SecondaryButtonText);
        Assert.Equal("開始", vm.PrimaryButtonText);
    }

    [Fact]
    public void WaitingForStream_Stop_DoesNotAskForConfirmation()
    {
        // 記録が何も無い状態なので、リセットと違って確認は不要
        var dialog = new FakeDialogService { ConfirmResult = false };
        var vm = NewVm(dialog);
        vm.StartCommand.Execute(null);

        vm.SecondaryCommand.Execute(null);

        Assert.Equal(AppState.Initial, vm.State);
    }

    [Fact]
    public void Recording_ShowsResetAndIsDisabled()
    {
        var vm = NewVm();
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.Equal(AppState.Recording, vm.State);
        Assert.Equal("リセット", vm.SecondaryButtonText);
        Assert.Same(vm.ResetCommand, vm.SecondaryCommand);
        Assert.False(vm.SecondaryCommand.CanExecute(null));
    }

    [Fact]
    public void RecordingEnded_ShowsResetAndIsEnabled()
    {
        var vm = NewVm();
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.StopCommand.Execute(null);

        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal("リセット", vm.SecondaryButtonText);
        Assert.Same(vm.ResetCommand, vm.SecondaryCommand);
        Assert.True(vm.SecondaryCommand.CanExecute(null));
    }

    [Fact]
    public void StateChange_RaisesSecondaryButtonNotifications()
    {
        var vm = NewVm();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.StartCommand.Execute(null);

        Assert.Contains(nameof(MainWindowViewModel.SecondaryButtonText), changed);
        Assert.Contains(nameof(MainWindowViewModel.SecondaryCommand), changed);
        Assert.Contains(nameof(MainWindowViewModel.SecondaryHintText), changed);
    }

    [Fact]
    public void SecondaryHintText_DescribesTheCurrentRole()
    {
        var vm = NewVm();
        Assert.Contains("リセット", vm.SecondaryHintText);

        vm.StartCommand.Execute(null);
        Assert.Contains("配信開始待ち", vm.SecondaryHintText);
    }
}
