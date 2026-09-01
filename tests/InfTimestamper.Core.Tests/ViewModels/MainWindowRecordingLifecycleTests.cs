using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>
/// 配信開始/終了時刻の確定、先頭の「配信開始」行、自動バックアップまわりの検証。
/// </summary>
public class MainWindowRecordingLifecycleTests
{
    private static TimestampEntry MakeEntry(string title, DateTimeOffset at)
    {
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = at };
        entry.SetField("title", title);
        return entry;
    }

    private static MainWindowViewModel BuildVm(
        AppStateMachine stateMachine,
        AppSettings settings,
        FakeClipboardService? clip = null,
        FakeDialogService? dialog = null,
        JsonRecordStore? store = null)
        => new(
            stateMachine,
            clip ?? new FakeClipboardService(),
            dialog ?? new FakeDialogService(),
            store ?? new JsonRecordStore(),
            settings,
            null,
            null);

    // --- 配信開始/終了時刻 -------------------------------------------------

    [Fact]
    public void DetectStreamStart_SetsStreamStartedAt()
    {
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault());

        vm.StartCommand.Execute(null);
        // OBS の配信開始検知に相当する遷移（強制開始ボタンを経由しない）
        sm.DetectStreamStart();

        Assert.Equal(AppState.Recording, vm.State);
        Assert.NotNull(vm.StreamStartedAt);
        Assert.NotEqual("-", vm.StreamStartedAtText);
    }

    [Fact]
    public void DetectStreamEnd_SetsStreamEndedAt()
    {
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault());

        vm.StartCommand.Execute(null);
        sm.DetectStreamStart();
        sm.DetectStreamEnd();

        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.NotNull(vm.Record.Stream.EndedAt);
    }

    [Fact]
    public void Resume_ClearsStreamEndedAt()
    {
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault());

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.StopCommand.Execute(null);
        Assert.NotNull(vm.Record.Stream.EndedAt);

        vm.ResumeCommand.Execute(null);
        Assert.Null(vm.Record.Stream.EndedAt);
    }

    // --- 配信開始時間の編集 ------------------------------------------------

    [Fact]
    public void EditStreamStartedAt_EnabledWhenMissingButEntriesExist()
    {
        var sm = new AppStateMachine();
        var dialog = new FakeDialogService();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault(), dialog: dialog);

        Assert.False(vm.EditStreamStartedAtCommand.CanExecute(null));

        var at = new DateTimeOffset(2026, 9, 1, 16, 0, 0, TimeSpan.FromHours(9));
        vm.AddTimestamp(MakeEntry("X", at));

        // 配信開始時間が "-" のままでも編集で入れ直せる
        Assert.Null(vm.StreamStartedAt);
        Assert.True(vm.EditStreamStartedAtCommand.CanExecute(null));

        var fixedStart = at.AddMinutes(-10);
        dialog.DateTimeEditorResult = new[] { fixedStart };
        vm.EditStreamStartedAtCommand.Execute(null);

        Assert.Equal(fixedStart, vm.StreamStartedAt);
        // 初期値には最初のプレイ記録の時刻が渡る
        Assert.Equal(at, dialog.LastDateTimeEditorInput![0]);
    }

    // --- 配信開始行 --------------------------------------------------------

    [Fact]
    public void StreamStartRow_IsFirstRowAndCopiedWithTimestamps()
    {
        var sm = new AppStateMachine();
        var clip = new FakeClipboardService();
        var settings = TestSettingsFactory.CreateDefault();
        var vm = BuildVm(sm, settings, clip);
        vm.Format = "$title";

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.AddTimestamp(MakeEntry("First", vm.StreamStartedAt!.Value.AddMinutes(1)));
        vm.AddTimestamp(MakeEntry("Second", vm.StreamStartedAt!.Value.AddMinutes(2)));

        Assert.Equal(3, vm.DisplayRows.Count);
        Assert.Equal("00:00:00 配信開始", vm.DisplayRows[0].DisplayText);
        Assert.Same(vm.Timestamps[0], vm.DisplayRows[1]);
        Assert.Same(vm.Timestamps[1], vm.DisplayRows[2]);

        vm.CopyCommand.Execute(null);
        var lines = clip.LastText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "00:00:00 配信開始", "First", "Second" }, lines);
    }

    [Fact]
    public void StreamStartRow_NotShownBeforeStreamStart()
    {
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault());

        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));

        Assert.Single(vm.DisplayRows);
        Assert.Same(vm.Timestamps[0], vm.DisplayRows[0]);
    }

    [Fact]
    public void StreamStartRow_CanBeDisabledBySettings()
    {
        var sm = new AppStateMachine();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.IncludeStreamStartRow = false;
        var vm = BuildVm(sm, settings);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.AddTimestamp(MakeEntry("X", vm.StreamStartedAt!.Value.AddMinutes(1)));

        Assert.Single(vm.DisplayRows);
        Assert.Same(vm.Timestamps[0], vm.DisplayRows[0]);
    }

    [Fact]
    public void StreamStartRow_UsesConfiguredLabel()
    {
        var sm = new AppStateMachine();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.StreamStartRowLabel = "配信開始・雑談";
        var vm = BuildVm(sm, settings);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);

        Assert.Equal("00:00:00 配信開始・雑談", vm.DisplayRows[0].DisplayText);
    }

    [Fact]
    public void DisplayRows_FollowReorderOfTimestamps()
    {
        var sm = new AppStateMachine();
        var dialog = new FakeDialogService();
        var vm = BuildVm(sm, TestSettingsFactory.CreateDefault(), dialog: dialog);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        var start = vm.StreamStartedAt!.Value;

        var first = MakeEntry("First", start.AddMinutes(1));
        var second = MakeEntry("Second", start.AddMinutes(2));
        vm.AddTimestamp(first);
        vm.AddTimestamp(second);

        // 2 件目を 1 件目より前へずらすと並びが入れ替わる
        vm.Timestamps[1].IsSelected = true;
        dialog.DateTimeEditorResult = new[] { start.AddSeconds(30) };
        vm.EditSelectedTimestampsCommand.Execute(null);

        Assert.Same(second, vm.Timestamps[0].Entry);
        Assert.IsType<StreamStartRowViewModel>(vm.DisplayRows[0]);
        Assert.Same(vm.Timestamps[0], vm.DisplayRows[1]);
        Assert.Same(vm.Timestamps[1], vm.DisplayRows[2]);
    }

    // --- 自動バックアップ --------------------------------------------------

    [Fact]
    public void AutoBackup_WritesRecordOnRecordingStartAndOnEachTimestamp()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.BackupDirectory = temp.Path;

        var sm = new AppStateMachine();
        var store = new JsonRecordStore();
        var vm = BuildVm(sm, settings, store: store);

        vm.StartCommand.Execute(null);
        sm.DetectStreamStart();

        var expected = Path.Combine(temp.Path,
            JsonRecordStore.GenerateFileName(GameId.Infinitas, vm.StreamStartedAt!.Value));
        Assert.True(File.Exists(expected));

        vm.AddTimestamp(MakeEntry("X", vm.StreamStartedAt!.Value.AddMinutes(1)));
        sm.DetectStreamEnd();

        var loaded = store.Load(expected);
        Assert.Equal(vm.StreamStartedAt, loaded.Stream.StartedAt);
        Assert.NotNull(loaded.Stream.EndedAt);
        Assert.Single(loaded.Timestamps);
    }

    [Fact]
    public void RequestExitConfirmation_AfterAutoBackup_DoesNotPrompt()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.BackupDirectory = temp.Path;
        settings.General.ConfirmOnExit = true;

        var sm = new AppStateMachine();
        var dialog = new FakeDialogService { ConfirmResult = false };
        var vm = BuildVm(sm, settings, dialog: dialog);

        vm.StartCommand.Execute(null);
        sm.DetectStreamStart();
        vm.AddTimestamp(MakeEntry("X", vm.StreamStartedAt!.Value.AddMinutes(1)));
        sm.DetectStreamEnd();

        // 自動バックアップ済みなので「未保存の記録があります」は出ない
        Assert.True(vm.RequestExitConfirmation());
    }

    [Fact]
    public void RequestExitConfirmation_WithoutBackupDirectory_PromptsOnUnsavedEntries()
    {
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.ConfirmOnExit = true;

        var sm = new AppStateMachine();
        var dialog = new FakeDialogService { ConfirmResult = false };
        var vm = BuildVm(sm, settings, dialog: dialog);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));
        vm.StopCommand.Execute(null);

        Assert.False(vm.RequestExitConfirmation());
    }

    [Fact]
    public void ManualSave_ClearsUnsavedFlag()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.ConfirmOnExit = true;

        var savePath = Path.Combine(temp.Path, "manual.json");
        var dialog = new FakeDialogService { ConfirmResult = false, SaveFileResult = savePath };
        var sm = new AppStateMachine();
        var vm = BuildVm(sm, settings, dialog: dialog);

        vm.StartCommand.Execute(null);
        vm.ForceStartCommand.Execute(null);
        vm.AddTimestamp(MakeEntry("X", DateTimeOffset.Now));
        vm.StopCommand.Execute(null);
        Assert.False(vm.RequestExitConfirmation());

        vm.SaveRecordCommand.Execute(null);

        Assert.True(File.Exists(savePath));
        Assert.True(vm.RequestExitConfirmation());
    }
}
