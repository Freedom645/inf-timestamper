using InfTimestamper.Core.Models;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

public class TimestampViewModelTests
{
    [Fact]
    public void DisplayText_FormatsRelativeTimestamp()
    {
        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        };
        entry.SetField("title", "TestSong");

        var streamStart = DateTimeOffset.UnixEpoch;
        var vm = new TimestampViewModel(entry, streamStart, "$timestamp $title");

        Assert.Equal("00:10:00 TestSong", vm.DisplayText);
    }

    [Fact]
    public void DisplayText_BeforeStreamStart_UsesNegativeSign()
    {
        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = DateTimeOffset.UnixEpoch,
        };
        entry.SetField("title", "X");

        var streamStart = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var vm = new TimestampViewModel(entry, streamStart, "$timestamp $title");

        Assert.StartsWith("-00:05:00", vm.DisplayText);
    }

    [Fact]
    public void UpdateFormat_RaisesPropertyChangedForDisplayText()
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = DateTimeOffset.Now };
        entry.SetField("title", "X");
        var vm = new TimestampViewModel(entry, DateTimeOffset.Now, "$title");

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.UpdateFormat("[$title]");

        Assert.Contains(nameof(TimestampViewModel.DisplayText), changes);
    }

    [Fact]
    public void UpdateStreamStartedAt_RecomputesTimestamp()
    {
        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = DateTimeOffset.UnixEpoch.AddMinutes(10),
        };
        var vm = new TimestampViewModel(entry, DateTimeOffset.UnixEpoch, "$timestamp");
        Assert.Equal("00:10:00", vm.DisplayText);

        vm.UpdateStreamStartedAt(DateTimeOffset.UnixEpoch.AddMinutes(5));
        Assert.Equal("00:05:00", vm.DisplayText);
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = DateTimeOffset.Now };
        var vm = new TimestampViewModel(entry, DateTimeOffset.Now, "$title");

        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.IsSelected = true;
        Assert.Contains(nameof(TimestampViewModel.IsSelected), changes);
        Assert.True(vm.IsSelected);
    }
}
