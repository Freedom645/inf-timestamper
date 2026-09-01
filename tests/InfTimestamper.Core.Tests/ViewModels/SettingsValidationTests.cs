using InfTimestamper.Core.Settings;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.ViewModels.Settings;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>要件「不正なパスや書込権限なしの場合は赤背景表示」「ホスト：IPアドレスv4形式」の判定。</summary>
public class SettingsValidationTests
{
    [Fact]
    public void BackupDirectory_ExistingWritableDirectory_IsValid()
    {
        using var temp = new TempDirectory();
        var vm = new GeneralSettingsViewModel(new GeneralSettings { BackupDirectory = temp.Path });

        Assert.True(vm.IsBackupDirectoryValid);
    }

    [Fact]
    public void BackupDirectory_NotYetCreatedButParentExists_IsValid()
    {
        using var temp = new TempDirectory();
        var vm = new GeneralSettingsViewModel(new GeneralSettings
        {
            BackupDirectory = Path.Combine(temp.Path, "backups"),
        });

        Assert.True(vm.IsBackupDirectoryValid);
    }

    [Fact]
    public void BackupDirectory_MissingParent_IsInvalid()
    {
        using var temp = new TempDirectory();
        var vm = new GeneralSettingsViewModel(new GeneralSettings
        {
            BackupDirectory = Path.Combine(temp.Path, "no-such-parent", "backups"),
        });

        Assert.False(vm.IsBackupDirectoryValid);
    }

    [Fact]
    public void BackupDirectory_Empty_IsInvalid()
    {
        var vm = new GeneralSettingsViewModel(new GeneralSettings { BackupDirectory = string.Empty });

        Assert.False(vm.IsBackupDirectoryValid);
    }

    [Fact]
    public void BackupDirectory_RaisesValidityWhenEdited()
    {
        using var temp = new TempDirectory();
        var vm = new GeneralSettingsViewModel(new GeneralSettings { BackupDirectory = string.Empty });

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.BackupDirectory = temp.Path;

        Assert.Contains(nameof(GeneralSettingsViewModel.IsBackupDirectoryValid), raised);
        Assert.True(vm.IsBackupDirectoryValid);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("192.168.1.111", true)]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("", false)]
    [InlineData("192.168.1", false)]
    [InlineData("obs.example.com", false)]
    [InlineData("::1", false)]
    public void Host_IsValidatedAsIpv4OrLocalhost(string host, bool expected)
    {
        var vm = new ObsSettingsViewModel(new ObsConnectionSettings { Host = host, Port = 4455 })
        {
            Host = host,
        };

        Assert.Equal(expected, vm.IsHostValid);
    }
}
