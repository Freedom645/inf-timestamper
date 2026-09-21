using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.ViewModels;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class SdvxSettingsViewModelTests
{
    private static SdvxSettingsViewModel Make(
        string format = "$timestamp $title", int port = 8767, string host = "127.0.0.1")
        => new(new SdvxSettings { TimestampFormat = format, HelperHost = host, HelperPort = port });

    [Fact]
    public void Preview_UpdatesReactivelyWithFormat()
    {
        var vm = Make("$title");
        Assert.Equal("Sample Song", vm.Preview);

        vm.TimestampFormat = "[$timestamp] $title ($grade)";
        Assert.Equal("[00:01:23] Sample Song (AA+)", vm.Preview);
    }

    [Fact]
    public void Endpoint_IsTheHelperWebSocketEndpoint()
    {
        var vm = Make(port: 8767);
        Assert.Equal("ws://127.0.0.1:8767", vm.Endpoint);

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.HelperPort = 9100;

        Assert.Equal("ws://127.0.0.1:9100", vm.Endpoint);
        Assert.Contains(nameof(SdvxSettingsViewModel.Endpoint), changes);
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
    public void Endpoint_FollowsTheHost()
    {
        var vm = Make();

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.HelperHost = "192.168.1.20";

        Assert.Equal("ws://192.168.1.20:8767", vm.Endpoint);
        Assert.Contains(nameof(SdvxSettingsViewModel.Endpoint), changes);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("localhost", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("192.168.1", false)]
    [InlineData("", false)]
    [InlineData("::1", false)]
    public void IsHelperHostValid_AcceptsIPv4OrLocalhost(string host, bool expected)
    {
        var vm = Make();
        vm.HelperHost = host;
        Assert.Equal(expected, vm.IsHelperHostValid);
    }

    [Fact]
    public void IsLocalhost_ResetsTheHostToTheDefault()
    {
        var vm = Make(host: "192.168.1.20");
        Assert.False(vm.IsLocalhost);

        vm.IsLocalhost = true;

        Assert.Equal(AppSettings.DefaultSdvxHelperHost, vm.HelperHost);
        Assert.True(vm.IsLocalhost);
    }

    [Fact]
    public void MissingHostAndPort_FallBackToTheDefaults()
    {
        var vm = new SdvxSettingsViewModel(new SdvxSettings
        {
            TimestampFormat = "$title",
            HelperHost = string.Empty,
            HelperPort = 0,
        });

        Assert.Equal(AppSettings.DefaultSdvxHelperHost, vm.HelperHost);
        Assert.Equal(AppSettings.DefaultSdvxHelperPort, vm.HelperPort);
    }

    [Fact]
    public void ToModel_PreservesAllFields()
    {
        var vm = Make("$timestamp");
        vm.HelperPort = 9100;

        vm.HelperHost = "192.168.1.20";

        var model = vm.ToModel();
        Assert.Equal("$timestamp", model.TimestampFormat);
        Assert.Equal("192.168.1.20", model.HelperHost);
        Assert.Equal(9100, model.HelperPort);
    }

    [Fact]
    public void HelperDirectory_RoundTripsThroughToModel()
    {
        var vm = new SdvxSettingsViewModel(new SdvxSettings
        {
            TimestampFormat = "$title",
            HelperDirectory = @"C:\sdvx_helper",
        });

        Assert.Equal(@"C:\sdvx_helper", vm.WatchDirectory);
        Assert.Equal(@"C:\sdvx_helper\log\sdvx_helper.log", vm.LogPath);

        vm.WatchDirectory = string.Empty;
        Assert.Equal(string.Empty, vm.LogPath);
        Assert.Equal(string.Empty, vm.ToModel().HelperDirectory);
    }

    [Fact]
    public void LogPath_RaisesWhenTheDirectoryChanges()
    {
        var vm = Make();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.WatchDirectory = @"C:\sdvx_helper";

        Assert.Contains(nameof(SdvxSettingsViewModel.LogPath), changes);
    }

    [Fact]
    public void BrowseWatchDirectory_AppliesSelectedFolder()
    {
        var dialog = new FakeDialogService { FolderBrowserResult = @"E:\sdvx_helper" };
        var vm = new SdvxSettingsViewModel(new SdvxSettings { TimestampFormat = "$title" }, dialog);

        vm.BrowseWatchDirectoryCommand.Execute(null);

        Assert.Equal(@"E:\sdvx_helper", vm.WatchDirectory);
    }

    [Fact]
    public void AvailableIdentifiers_MatchesSdvxIdentifiers()
    {
        var vm = Make();
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "timestamp");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "title");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "diff_l");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "diff_s");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "level");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "grade");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "clear_lamp");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "score");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "score_short");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "ex_score");
    }
}
