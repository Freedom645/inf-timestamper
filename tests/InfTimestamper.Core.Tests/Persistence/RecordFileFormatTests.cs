using System.Text;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Tests.TestHelpers;
using NUlid;

namespace InfTimestamper.Core.Tests.Persistence;

/// <summary>要件「バックアップファイル」「日時フォーマット」「フィールド定義」の出力仕様。</summary>
public class RecordFileFormatTests
{
    private static StreamRecord MakeRecord()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 20, 0, 0, 123, TimeSpan.FromHours(9));
        var record = new StreamRecord
        {
            Game = GameId.Infinitas,
            Stream = new StreamInfo { StartedAt = startedAt, EndedAt = startedAt.AddHours(2) },
        };
        var entry = new TimestampEntry { Id = Ulid.NewUlid(), PlayStartedAt = startedAt.AddMinutes(1) };
        entry.SetField("title", "GIGA RAID");
        entry.SetField("level", 11);
        entry.SetField("ex_score", 1820);
        record.Timestamps.Add(entry);
        return record;
    }

    private static string SaveAndRead(StreamRecord record, TempDirectory temp, JsonRecordStore? store = null)
    {
        var path = Path.Combine(temp.Path, "format.json");
        (store ?? new JsonRecordStore()).SaveAtomic(record, path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void SaveAtomic_WritesUtf8WithoutBom()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "format.json");
        new JsonRecordStore().SaveAtomic(MakeRecord(), path);

        var head = File.ReadAllBytes(path).Take(3).ToArray();
        Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, head);
    }

    [Fact]
    public void SaveAtomic_UsesLineFeedOnly()
    {
        using var temp = new TempDirectory();
        var text = SaveAndRead(MakeRecord(), temp);

        Assert.DoesNotContain("\r", text);
        Assert.Contains("\n", text);
    }

    [Fact]
    public void SaveAtomic_WritesSecondPrecisionTimestamps()
    {
        using var temp = new TempDirectory();
        var text = SaveAndRead(MakeRecord(), temp);

        Assert.Contains("\"startedAt\": \"2026-09-01T20:00:00+09:00\"", text);
        Assert.Contains("\"playStartedAt\": \"2026-09-01T20:01:00+09:00\"", text);
    }

    [Fact]
    public void SaveAtomic_WritesNumericFieldsAsNumbers()
    {
        using var temp = new TempDirectory();
        var text = SaveAndRead(MakeRecord(), temp);

        Assert.Contains("\"level\": 11", text);
        Assert.Contains("\"ex_score\": 1820", text);
        Assert.Contains("\"title\": \"GIGA RAID\"", text);
    }

    [Fact]
    public void SaveAtomic_StampsConfiguredAppVersion()
    {
        using var temp = new TempDirectory();
        var store = new JsonRecordStore(
            InfTimestamper.Core.Persistence.Json.JsonOptionsFactory.CreateRecordOptions(), "2.3.4");

        var record = MakeRecord();
        record.App.Version = "0.0.1";
        var text = SaveAndRead(record, temp, store);

        Assert.Contains("\"version\": \"2.3.4\"", text);
        Assert.Contains("\"name\": \"inf-timestamper\"", text);
    }

    [Fact]
    public void Load_AcceptsSubSecondTimestampsWrittenByOlderVersions()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "legacy.json");
        var json = """
        {
          "schemaVersion": 1,
          "app": { "name": "inf-timestamper", "version": "1.0.0" },
          "game": "INFINITAS",
          "stream": {
            "startedAt": "2026-09-01T15:51:04.1096635+09:00",
            "endedAt": null
          },
          "createdAt": "2026-09-01T15:51:04.1096635+09:00",
          "updatedAt": "2026-09-01T15:51:04.1096635+09:00",
          "timestamps": []
        }
        """;
        File.WriteAllText(path, json, new UTF8Encoding(false));

        var loaded = new JsonRecordStore().Load(path);

        Assert.Equal(
            new DateTimeOffset(2026, 9, 1, 15, 51, 4, TimeSpan.FromHours(9)).AddTicks(1096635),
            loaded.Stream.StartedAt);
    }
}
