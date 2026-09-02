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
}
