using InfTimestamper.Core.Settings;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class ObsSettingsViewModelTests
{
    [Fact]
    public void IsLocalhost_DetectsLoopback()
    {
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings { Host = "127.0.0.1", Port = 4455 });
        Assert.True(vm.IsLocalhost);

        vm.Host = "192.168.1.1";
        Assert.False(vm.IsLocalhost);
    }

    [Fact]
    public void SettingIsLocalhostTrue_ResetsHostToLoopback()
    {
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings { Host = "10.0.0.1", Port = 4455 });
        vm.IsLocalhost = true;
        Assert.Equal("127.0.0.1", vm.Host);
        Assert.True(vm.IsLocalhost);
    }

    [Fact]
    public void ToModel_RoundTrips()
    {
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings { Host = "host", Port = 1234, Password = "pw" });
        var model = vm.ToModel();

        Assert.Equal("host", model.Host);
        Assert.Equal(1234, model.Port);
        Assert.Equal("pw", model.Password);
    }
}
