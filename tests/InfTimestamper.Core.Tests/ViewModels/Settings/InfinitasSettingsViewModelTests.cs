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
        vm.RefluxDirectory = @"D:\games\reflux";

        var model = vm.ToModel();
        Assert.Equal("$timestamp", model.TimestampFormat);
        Assert.Equal(@"D:\games\reflux", model.RefluxDirectory);
    }

    [Fact]
    public void BrowseRefluxDirectory_AppliesSelectedFolder()
    {
        var dialog = new FakeDialogService { FolderBrowserResult = @"E:\reflux\out" };
        var vm = new InfinitasSettingsViewModel(
            new InfinitasSettings { TimestampFormat = "$timestamp", RefluxDirectory = "old" },
            dialog);

        vm.BrowseRefluxDirectoryCommand.Execute(null);

        Assert.Equal(@"E:\reflux\out", vm.RefluxDirectory);
    }

    [Fact]
    public void AvailableIdentifiers_MatchesSupportedKeys()
    {
        var vm = Make();
        Assert.Contains("timestamp", vm.AvailableIdentifiers);
        Assert.Contains("title", vm.AvailableIdentifiers);
        Assert.Contains("diff_l", vm.AvailableIdentifiers);
        Assert.Contains("diff_s", vm.AvailableIdentifiers);
        Assert.Contains("level", vm.AvailableIdentifiers);
        Assert.Contains("miss_count", vm.AvailableIdentifiers);
        Assert.Contains("ex_score", vm.AvailableIdentifiers);
        Assert.Contains("dj_level", vm.AvailableIdentifiers);
        Assert.Contains("lamp", vm.AvailableIdentifiers);
    }
}
