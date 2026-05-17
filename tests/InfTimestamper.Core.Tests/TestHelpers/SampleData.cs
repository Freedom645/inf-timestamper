using InfTimestamper.Core.Models;
using NUlid;

namespace InfTimestamper.Core.Tests.TestHelpers;

internal static class SampleData
{
    public static StreamRecord MakeRecord(DateTimeOffset? endedAt = null)
    {
        var startedAt = new DateTimeOffset(2026, 5, 7, 20, 0, 0, TimeSpan.FromHours(9));
        var record = new StreamRecord
        {
            App = new AppInfo { Name = AppInfo.DefaultName, Version = "1.0.0" },
            Game = GameId.Infinitas,
            Stream = new StreamInfo { StartedAt = startedAt, EndedAt = endedAt },
            CreatedAt = startedAt,
            UpdatedAt = startedAt,
        };

        var entry = new TimestampEntry
        {
            Id = Ulid.NewUlid(),
            PlayStartedAt = startedAt.AddMinutes(1),
        };
        entry.SetField("title", "GIGA RAID");
        entry.SetField("level", 11);
        entry.SetField("diff_s", "SPA");
        record.Timestamps.Add(entry);

        return record;
    }
}
