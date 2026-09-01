using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.Services;
using InfTimestamper.ViewModels;
using NUlid;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>異常終了復旧（要件「異常終了からの復旧」）の 3 択と複数提示の検証。</summary>
public class MainWindowRecoveryTests
{
    private sealed class FakeFileRecycler : IFileRecycler
    {
        public List<string> Recycled { get; } = new();

        public void SendToRecycleBin(string path)
        {
            Recycled.Add(path);
            File.Delete(path);
        }
    }

    private static string SaveUnfinished(JsonRecordStore store, string dir, DateTimeOffset startedAt, int entries)
    {
        var record = new StreamRecord
        {
            Game = GameId.Infinitas,
            Stream = new StreamInfo { StartedAt = startedAt, EndedAt = null },
        };
        for (var i = 0; i < entries; i++)
        {
            var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = startedAt.AddMinutes(i + 1) };
            entry.SetField("title", $"Song {i}");
            record.Timestamps.Add(entry);
        }

        var path = Path.Combine(dir, JsonRecordStore.GenerateFileName(GameId.Infinitas, startedAt));
        store.SaveAtomic(record, path);
        return path;
    }

    private static MainWindowViewModel BuildVm(
        AppSettings settings, FakeDialogService dialog, JsonRecordStore store, IFileRecycler? recycler)
        => new(
            new AppStateMachine(),
            new FakeClipboardService(),
            dialog,
            store,
            settings,
            null,
            null,
            null,
            null,
            null,
            recycler);

    [Fact]
    public void CheckUnfinishedRecords_PresentsEveryCandidateNewestFirst()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.BackupDirectory = temp.Path;

        var store = new JsonRecordStore();
        var older = SaveUnfinished(store, temp.Path, new DateTimeOffset(2026, 8, 30, 20, 0, 0, TimeSpan.FromHours(9)), 1);
        var newer = SaveUnfinished(store, temp.Path, new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.FromHours(9)), 3);
        File.SetLastWriteTime(older, DateTime.Now.AddHours(-2));
        File.SetLastWriteTime(newer, DateTime.Now);

        var dialog = new FakeDialogService();
        dialog.UnfinishedChoices.Add(UnfinishedRecordChoice.Ignore);
        dialog.UnfinishedChoices.Add(UnfinishedRecordChoice.Ignore);

        var vm = BuildVm(settings, dialog, store, new FakeFileRecycler());
        vm.CheckUnfinishedRecords();

        // 新しい順に、無視された分もすべて提示される
        Assert.Equal(2, dialog.UnfinishedPrompts.Count);
        Assert.Equal(newer, dialog.UnfinishedPrompts[0].FilePath);
        Assert.Equal(older, dialog.UnfinishedPrompts[1].FilePath);
        Assert.Equal(AppState.Initial, vm.State);
    }

    [Fact]
    public void CheckUnfinishedRecords_DeleteSendsFileToRecycleBinAndContinues()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.BackupDirectory = temp.Path;

        var store = new JsonRecordStore();
        var older = SaveUnfinished(store, temp.Path, new DateTimeOffset(2026, 8, 30, 20, 0, 0, TimeSpan.FromHours(9)), 1);
        var newer = SaveUnfinished(store, temp.Path, new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.FromHours(9)), 3);
        File.SetLastWriteTime(older, DateTime.Now.AddHours(-2));
        File.SetLastWriteTime(newer, DateTime.Now);

        var dialog = new FakeDialogService();
        dialog.UnfinishedChoices.Add(UnfinishedRecordChoice.Delete); // newer
        dialog.UnfinishedChoices.Add(UnfinishedRecordChoice.Load);   // older

        var recycler = new FakeFileRecycler();
        var vm = BuildVm(settings, dialog, store, recycler);
        vm.CheckUnfinishedRecords();

        Assert.Equal(new[] { newer }, recycler.Recycled);
        Assert.False(File.Exists(newer));
        // 削除しても走査は続き、次の候補を読み込める
        Assert.Equal(AppState.RecordingEnded, vm.State);
        Assert.Equal(1, vm.TimestampCount);
    }

    [Fact]
    public void CheckUnfinishedRecords_ExposesFileInfoToTheDialog()
    {
        using var temp = new TempDirectory();
        var settings = TestSettingsFactory.CreateDefault();
        settings.General.BackupDirectory = temp.Path;

        var store = new JsonRecordStore();
        var path = SaveUnfinished(store, temp.Path, new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.FromHours(9)), 4);

        var dialog = new FakeDialogService();
        dialog.UnfinishedChoices.Add(UnfinishedRecordChoice.Ignore);

        var vm = BuildVm(settings, dialog, store, new FakeFileRecycler());
        vm.CheckUnfinishedRecords();

        var prompted = Assert.Single(dialog.UnfinishedPrompts);
        Assert.Equal(path, prompted.FilePath);
        Assert.Equal(4, prompted.Record.Timestamps.Count);
        Assert.NotEqual(default, prompted.LastModified);
    }
}
