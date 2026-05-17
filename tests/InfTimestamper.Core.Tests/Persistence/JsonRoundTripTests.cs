using System.Text.Json;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence.Json;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Persistence;

public class JsonRoundTripTests
{
    private static readonly JsonSerializerOptions Options = JsonOptionsFactory.CreateRecordOptions();

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = SampleData.MakeRecord(endedAt: new DateTimeOffset(2026, 5, 7, 22, 0, 0, TimeSpan.FromHours(9)));
        var json = JsonSerializer.Serialize(original, Options);
        var loaded = JsonSerializer.Deserialize<StreamRecord>(json, Options)!;

        Assert.Equal(original.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(original.App.Name, loaded.App.Name);
        Assert.Equal(original.App.Version, loaded.App.Version);
        Assert.Equal(original.Game, loaded.Game);
        Assert.Equal(original.Stream.StartedAt, loaded.Stream.StartedAt);
        Assert.Equal(original.Stream.EndedAt, loaded.Stream.EndedAt);
        Assert.Equal(original.Timestamps.Count, loaded.Timestamps.Count);
        Assert.Equal(original.Timestamps[0].Id, loaded.Timestamps[0].Id);
        Assert.Equal(original.Timestamps[0].PlayStartedAt, loaded.Timestamps[0].PlayStartedAt);

        Assert.True(loaded.Timestamps[0].TryGetFieldAsString("title", out var title));
        Assert.Equal("GIGA RAID", title);
        Assert.True(loaded.Timestamps[0].TryGetFieldAsString("level", out var level));
        Assert.Equal("11", level);
    }

    [Fact]
    public void SerializedJson_UsesCamelCaseAndIso8601()
    {
        var record = SampleData.MakeRecord();
        var json = JsonSerializer.Serialize(record, Options);

        Assert.Contains("\"schemaVersion\":", json);
        Assert.Contains("\"createdAt\":", json);
        Assert.Contains("\"playStartedAt\":", json);
        Assert.Contains("\"game\": \"INFINITAS\"", json);
        // ISO 8601 with offset (e.g. "+09:00")
        Assert.Matches(@"\""20\d{2}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+\-]\d{2}:\d{2}\""", json);
    }

    [Fact]
    public void EndedAtNull_SerializesAsJsonNull()
    {
        var record = SampleData.MakeRecord(endedAt: null);
        var json = JsonSerializer.Serialize(record, Options);
        Assert.Contains("\"endedAt\": null", json);
    }

    [Fact]
    public void UnknownFieldKey_IsPreservedAcrossRoundTrip()
    {
        const string source = """
        {
          "schemaVersion": 1,
          "app": { "name": "inf-timestamper", "version": "1.0.0" },
          "game": "INFINITAS",
          "stream": { "startedAt": "2026-05-07T20:00:00+09:00", "endedAt": null },
          "createdAt": "2026-05-07T20:00:00+09:00",
          "updatedAt": "2026-05-07T20:00:00+09:00",
          "timestamps": [
            {
              "id": "01HZ7M5R3K8X4N9PY7QW2BVD8C",
              "playStartedAt": "2026-05-07T20:01:15+09:00",
              "fields": { "future_key": "future_value", "title": "X" }
            }
          ]
        }
        """;

        var record = JsonSerializer.Deserialize<StreamRecord>(source, Options)!;
        Assert.True(record.Timestamps[0].Fields.ContainsKey("future_key"));

        var roundTrip = JsonSerializer.Serialize(record, Options);
        Assert.Contains("future_key", roundTrip);
        Assert.Contains("future_value", roundTrip);
    }

    [Fact]
    public void UnknownGameValue_ThrowsUnknownGameException()
    {
        const string source = """
        {
          "schemaVersion": 1,
          "app": { "name": "x", "version": "0" },
          "game": "MYSTERY_GAME",
          "stream": { "startedAt": "2026-01-01T00:00:00+09:00", "endedAt": null },
          "createdAt": "2026-01-01T00:00:00+09:00",
          "updatedAt": "2026-01-01T00:00:00+09:00",
          "timestamps": []
        }
        """;

        Assert.Throws<UnknownGameException>(() => JsonSerializer.Deserialize<StreamRecord>(source, Options));
    }
}
