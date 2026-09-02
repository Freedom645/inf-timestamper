using InfTimestamper.Core.Settings;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class SdvxSettingsViewModelTests
{
    private static SdvxSettingsViewModel Make(string format = "$timestamp $title", int port = 8767)
        => new(new SdvxSettings { TimestampFormat = format, HelperPort = port });

    [Fact]
    public void Preview_UpdatesReactivelyWithFormat()
    {
        var vm = Make("$title");
        Assert.Equal("Sample Song", vm.Preview);

        vm.TimestampFormat = "[$timestamp] $title ($grade)";
        Assert.Equal("[00:01:23] Sample Song (AA+)", vm.Preview);
    }

    [Fact]
    public void WatchTarget_IsTheHelperWebSocketEndpoint()
    {
        var vm = Make(port: 8767);
        Assert.Equal("ws://127.0.0.1:8767", vm.WatchTarget);

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.HelperPort = 9100;

        Assert.Equal("ws://127.0.0.1:9100", vm.WatchTarget);
        Assert.Contains(nameof(SdvxSettingsViewModel.WatchTarget), changes);
    }

    [Theory]
    [InlineData(8767, true)]
    [InlineData(1, true)]
    [InlineData(65535, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(65536, false)]
    public void IsHelperPortValid_ChecksTheRange(int port, bool expected)
    {
        var vm = Make();
        vm.HelperPort = port;
        Assert.Equal(expected, vm.IsHelperPortValid);
    }

    [Fact]
    public void MissingPort_FallsBackToTheDefault()
    {
        var vm = new SdvxSettingsViewModel(new SdvxSettings { TimestampFormat = "$title", HelperPort = 0 });
        Assert.Equal(AppSettings.DefaultSdvxHelperPort, vm.HelperPort);
    }

    [Fact]
    public void ToModel_PreservesAllFields()
    {
        var vm = Make("$timestamp");
        vm.HelperPort = 9100;

        var model = vm.ToModel();
        Assert.Equal("$timestamp", model.TimestampFormat);
        Assert.Equal(9100, model.HelperPort);
    }

    [Fact]
    public void AvailableIdentifiers_MatchesSdvxIdentifiers()
    {
        var vm = Make();
        Assert.Contains("timestamp", vm.AvailableIdentifiers);
        Assert.Contains("title", vm.AvailableIdentifiers);
        Assert.Contains("diff_l", vm.AvailableIdentifiers);
        Assert.Contains("diff_s", vm.AvailableIdentifiers);
        Assert.Contains("level", vm.AvailableIdentifiers);
        Assert.Contains("grade", vm.AvailableIdentifiers);
        Assert.Contains("clear_lamp", vm.AvailableIdentifiers);
        Assert.Contains("score", vm.AvailableIdentifiers);
        Assert.Contains("score_short", vm.AvailableIdentifiers);
        Assert.Contains("ex_score", vm.AvailableIdentifiers);
    }
}
