using InfTimestamper.Core.Settings;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class SettingsDialogViewModelTests
{
    [Fact]
    public void Confirm_ProducesResultThatRoundTripsValues()
    {
        var initial = AppSettings.CreateDefault();
        initial.Obs.Host = "10.0.0.1";
        initial.Obs.Port = 4500;
        initial.Infinitas.TimestampFormat = "$title";

        var vm = new SettingsDialogViewModel(initial);
        vm.Obs.Host = "192.168.1.5";
        vm.Infinitas.TimestampFormat = "$timestamp $title";
        vm.General.AutoUpdateCheck = false;
        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.DialogResult);
        Assert.NotNull(vm.Result);
        Assert.Equal("192.168.1.5", vm.Result!.Obs.Host);
        Assert.Equal(4500, vm.Result.Obs.Port);
        Assert.Equal("$timestamp $title", vm.Result.Infinitas.TimestampFormat);
        Assert.False(vm.Result.General.AutoUpdateCheck);
    }

    [Fact]
    public void Cancel_LeavesResultNull()
    {
        var vm = new SettingsDialogViewModel(AppSettings.CreateDefault());
        vm.Obs.Host = "modified";
        vm.CancelCommand.Execute(null);

        Assert.False(vm.DialogResult);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void RequestClose_FiresOnConfirmAndCancel()
    {
        var vm1 = new SettingsDialogViewModel(AppSettings.CreateDefault());
        int closeFires1 = 0;
        vm1.RequestClose += () => closeFires1++;
        vm1.ConfirmCommand.Execute(null);
        Assert.Equal(1, closeFires1);

        var vm2 = new SettingsDialogViewModel(AppSettings.CreateDefault());
        int closeFires2 = 0;
        vm2.RequestClose += () => closeFires2++;
        vm2.CancelCommand.Execute(null);
        Assert.Equal(1, closeFires2);
    }
}
