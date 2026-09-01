using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>
/// タイムスタンプの選択まわり。ListBox の選択（Shift/Ctrl クリック）は
/// <c>ListBoxItem.IsSelected</c> 経由で行の <c>IsSelected</c> に入るので、
/// ここでは行の <c>IsSelected</c> を直接動かして VM 側の追従を検証する。
/// </summary>
public class TimestampSelectionTests
{
    private static MainWindowViewModel NewVm(FakeDialogService? dialog = null)
        => new(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog ?? new FakeDialogService(),
            new JsonRecordStore(),
            TestSettingsFactory.CreateDefault(),
            null,
            null);

    private static TimestampEntry MakeEntry(string title, DateTimeOffset at)
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = at };
        entry.SetField("title", title);
        return entry;
    }

    [Fact]
    public void SelectingRow_EnablesEditCommandWithoutExplicitNotification()
    {
        var vm = NewVm();
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));

        var raised = 0;
        vm.EditSelectedTimestampsCommand.CanExecuteChanged += (_, _) => raised++;

        Assert.False(vm.EditSelectedTimestampsCommand.CanExecute(null));

        vm.Timestamps[0].IsSelected = true;

        Assert.True(raised > 0);
        Assert.True(vm.EditSelectedTimestampsCommand.CanExecute(null));

        vm.Timestamps[0].IsSelected = false;
        Assert.False(vm.EditSelectedTimestampsCommand.CanExecute(null));
    }

    [Fact]
    public void SelectionSubscription_SurvivesReordering()
    {
        var dialog = new FakeDialogService();
        var vm = NewVm(dialog);
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        var start = vm.StreamStartedAt!.Value;

        vm.AddTimestamp(MakeEntry("First", start.AddMinutes(1)));
        vm.AddTimestamp(MakeEntry("Second", start.AddMinutes(2)));

        // 2 件目を前へずらして並べ替え（Clear → 再追加）を起こす
        vm.Timestamps[1].IsSelected = true;
        dialog.DateTimeEditorResult = new[] { start.AddSeconds(30) };
        vm.EditSelectedTimestampsCommand.Execute(null);

        foreach (var row in vm.Timestamps) row.IsSelected = false;

        var raised = 0;
        vm.EditSelectedTimestampsCommand.CanExecuteChanged += (_, _) => raised++;
        vm.Timestamps[0].IsSelected = true;

        // 並べ替え後も購読が 1 本だけ生きている
        Assert.Equal(1, raised);
        Assert.True(vm.EditSelectedTimestampsCommand.CanExecute(null));
    }

    [Fact]
    public void StreamStartRow_IsNotSelectable()
    {
        var vm = NewVm();
        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        var header = Assert.IsType<StreamStartRowViewModel>(vm.DisplayRows[0]);
        Assert.False(header.IsSelectable);

        // ListBoxItem からの書込みを無視するので、範囲選択に巻き込まれても編集対象にならない
        header.IsSelected = true;
        Assert.False(header.IsSelected);
    }
}
