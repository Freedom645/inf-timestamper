using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Persistence;

public class JsonRecordStoreTests
{
    [Fact]
    public void GenerateFileName_MatchesSpecification()
    {
        var when = new DateTimeOffset(2026, 5, 7, 20, 0, 0, TimeSpan.FromHours(9));
        var name = JsonRecordStore.GenerateFileName(GameId.Infinitas, when);
        Assert.Equal("INFINITAS_20260507_200000.json", name);
    }

    [Fact]
    public void SaveAtomic_WritesAndCleansUpTempAndBak()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "test.json");
        var store = new JsonRecordStore();
        var record = SampleData.MakeRecord();

        store.SaveAtomic(record, path);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void SaveAtomic_OverwritesExistingFileSafely()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "test.json");
        var store = new JsonRecordStore();
        var record1 = SampleData.MakeRecord();
        store.SaveAtomic(record1, path);

        var record2 = SampleData.MakeRecord();
        record2.Timestamps[0].SetField("title", "Updated Title");
        store.SaveAtomic(record2, path);

        var loaded = store.Load(path);
        Assert.True(loaded.Timestamps[0].TryGetFieldAsString("title", out var title));
        Assert.Equal("Updated Title", title);
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void SaveAtomic_UpdatesUpdatedAtTimestamp()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "test.json");
        var store = new JsonRecordStore();
        var record = SampleData.MakeRecord();
        record.UpdatedAt = DateTimeOffset.MinValue;

        store.SaveAtomic(record, path);

        Assert.NotEqual(DateTimeOffset.MinValue, record.UpdatedAt);
    }

    [Fact]
    public void Load_RoundTripsAfterSave()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "test.json");
        var store = new JsonRecordStore();
        var original = SampleData.MakeRecord();
        store.SaveAtomic(original, path);

        var loaded = store.Load(path);
        Assert.Equal(original.Game, loaded.Game);
        Assert.Equal(original.Timestamps[0].Id, loaded.Timestamps[0].Id);
    }

    [Fact]
    public void Load_ThrowsIncompatibleSchemaException_WhenSchemaTooNew()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "test.json");
        File.WriteAllText(path, """
        {
          "schemaVersion": 99,
          "app": { "name": "x", "version": "0" },
          "game": "INFINITAS",
          "stream": { "startedAt": "2026-01-01T00:00:00+09:00", "endedAt": null },
          "createdAt": "2026-01-01T00:00:00+09:00",
          "updatedAt": "2026-01-01T00:00:00+09:00",
          "timestamps": []
        }
        """);
        var store = new JsonRecordStore();
        var ex = Assert.Throws<IncompatibleSchemaException>(() => store.Load(path));
        Assert.Equal(99, ex.FileSchemaVersion);
    }

    [Fact]
    public void Load_ThrowsInvalidDataException_WhenJsonIsCorrupt()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "broken.json");
        File.WriteAllText(path, "{ not valid json");
        var store = new JsonRecordStore();
        Assert.Throws<InvalidDataException>(() => store.Load(path));
    }

    [Fact]
    public void FindUnfinished_ReturnsOnlyRecordsWithNullEndedAt()
    {
        using var temp = new TempDirectory();
        var store = new JsonRecordStore();

        var unfinishedPath = Path.Combine(temp.Path, "unfinished.json");
        var unfinished = SampleData.MakeRecord(endedAt: null);
        store.SaveAtomic(unfinished, unfinishedPath);

        var finishedPath = Path.Combine(temp.Path, "finished.json");
        var finished = SampleData.MakeRecord(endedAt: DateTimeOffset.Now);
        store.SaveAtomic(finished, finishedPath);

        var results = store.FindUnfinished(temp.Path).ToList();
        Assert.Single(results);
        Assert.Equal(unfinishedPath, results[0].FilePath);
    }

    [Fact]
    public void FindUnfinished_ReturnsEmptyForMissingDirectory()
    {
        var store = new JsonRecordStore();
        var results = store.FindUnfinished(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())).ToList();
        Assert.Empty(results);
    }

    [Fact]
    public void FindUnfinished_SkipsCorruptFiles()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "broken.json"), "garbage");
        var store = new JsonRecordStore();
        var results = store.FindUnfinished(temp.Path).ToList();
        Assert.Empty(results);
    }
}
