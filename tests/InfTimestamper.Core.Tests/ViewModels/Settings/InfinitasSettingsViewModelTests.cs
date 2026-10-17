using InfTimestamper.Core.Games;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.ViewModels;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels.Settings;

public class InfinitasSettingsViewModelTests
{
    private static InfinitasSettingsViewModel Make(string format = "$timestamp $title")
    {
        return new InfinitasSettingsViewModel(new InfinitasSettings
        {
            TimestampFormat = format,
            RefluxDirectory = @"C:\reflux",
        });
    }

    [Fact]
    public void Preview_UpdatesReactivelyWithFormat()
    {
        var vm = Make("$title");
        Assert.Equal("Sample Song", vm.Preview);

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.TimestampFormat = "[$timestamp] $title";
        Assert.Equal("[00:01:23] Sample Song", vm.Preview);
        Assert.Contains(nameof(InfinitasSettingsViewModel.Preview), changes);
    }

    [Theory]
    [InlineData(0, "title", "$title$timestamp")]
    [InlineData(10, "title", "$timestamp$title")]
    [InlineData(100, "title", "$timestamp$title")] // 範囲超過は末尾にクランプ
    [InlineData(5, "diff_l", "$time$diff_lstamp")]
    public void InsertIdentifierAtCursor_InsertsAtPosition(int pos, string id, string expected)
    {
        var vm = Make("$timestamp");
        vm.InsertIdentifierAtCursor(pos, id);
        Assert.Equal(expected, vm.TimestampFormat);
    }

    [Fact]
    public void ToModel_PreservesAllFields()
    {
        var vm = Make("$timestamp");
        vm.WatchDirectory = @"D:\games\reflux";

        var model = vm.ToModel();
        Assert.Equal("$timestamp", model.TimestampFormat);
        Assert.Equal(@"D:\games\reflux", model.RefluxDirectory);
    }

    [Fact]
    public void BrowseWatchDirectory_AppliesSelectedFolder()
    {
        var dialog = new FakeDialogService { FolderBrowserResult = @"E:\reflux\out" };
        var vm = new InfinitasSettingsViewModel(
            new InfinitasSettings { TimestampFormat = "$timestamp", RefluxDirectory = "old" },
            dialog);

        vm.BrowseWatchDirectoryCommand.Execute(null);

        Assert.Equal(@"E:\reflux\out", vm.WatchDirectory);
    }

    [Fact]
    public void AvailableIdentifiers_MatchesInfinitasIdentifiers()
    {
        var vm = Make();
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "timestamp");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "title");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "diff_l");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "diff_s");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "level");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "miss_count");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "ex_score");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "dj_level");
        Assert.Contains(vm.AvailableIdentifiers, c => c.Key == "lamp");
    }

    // ---- プレイの検知元（Reflux / 打鍵カウンタ。issue #13） ----

    [Fact]
    public void PlaySource_DefaultsToReflux()
    {
        var vm = Make();

        Assert.Equal(PlaySourceId.Reflux, vm.PlaySource);
        Assert.True(vm.IsRefluxSource);
        Assert.False(vm.IsDakenCounterSource);
    }

    [Fact]
    public void SelectingDakenCounter_SwitchesRadioButtonsAndModel()
    {
        var vm = Make();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.IsDakenCounterSource = true;

        Assert.Equal(PlaySourceId.DakenCounter, vm.PlaySource);
        Assert.False(vm.IsRefluxSource);
        Assert.Contains(nameof(InfinitasSettingsViewModel.IsRefluxSource), changes);
        Assert.Contains(nameof(InfinitasSettingsViewModel.IsDakenCounterSource), changes);
        Assert.Equal(PlaySourceIdExtensions.DakenCounterSerialized, vm.ToModel().PlaySource);
    }

    [Fact]
    public void ToModel_KeepsSettingsOfTheUnselectedSource()
    {
        // 検知元を切り替えても、選んでいない側の設定は消さない（戻したときに入れ直さなくて済むように）
        var vm = new InfinitasSettingsViewModel(new InfinitasSettings
        {
            TimestampFormat = "$timestamp",
            RefluxDirectory = @"C:eflux",
            PlaySource = PlaySourceIdExtensions.DakenCounterSerialized,
            DakenCounterHost = "192.168.1.10",
            DakenCounterPort = 9000,
        });

        vm.IsRefluxSource = true;
        var model = vm.ToModel();

        Assert.Equal(PlaySourceIdExtensions.RefluxSerialized, model.PlaySource);
        Assert.Equal(@"C:eflux", model.RefluxDirectory);
        Assert.Equal("192.168.1.10", model.DakenCounterHost);
        Assert.Equal(9000, model.DakenCounterPort);
    }

    [Fact]
    public void DakenCounterEndpoint_FollowsHostAndPort()
    {
        var vm = Make();
        Assert.Equal("ws://127.0.0.1:8767", vm.DakenCounterEndpoint);
        Assert.True(vm.IsDakenCounterLocalhost);

        vm.DakenCounterHost = "192.168.1.10";
        vm.DakenCounterPort = 9000;

        Assert.Equal("ws://192.168.1.10:9000", vm.DakenCounterEndpoint);
        Assert.False(vm.IsDakenCounterLocalhost);

        vm.IsDakenCounterLocalhost = true;
        Assert.Equal("127.0.0.1", vm.DakenCounterHost);
    }

    [Theory]
    [InlineData("127.0.0.1", 8767, true, true)]
    [InlineData("192.168.1.10", 1, true, true)]
    [InlineData("192.168.1", 8767, false, true)]
    [InlineData("127.0.0.1", 0, true, false)]
    [InlineData("127.0.0.1", 65536, true, false)]
    public void DakenCounterValidity(string host, int port, bool hostValid, bool portValid)
    {
        var vm = Make();
        vm.DakenCounterHost = host;
        vm.DakenCounterPort = port;

        Assert.Equal(hostValid, vm.IsDakenCounterHostValid);
        Assert.Equal(portValid, vm.IsDakenCounterPortValid);
    }
}
