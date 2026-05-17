using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Recognition;

public sealed class SongRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private readonly List<SongRecord> _records;

    public SongRepository(IEnumerable<SongRecord> records)
    {
        _records = records?.ToList() ?? throw new ArgumentNullException(nameof(records));
    }

    public IReadOnlyList<SongRecord> All => _records;

    public int Count => _records.Count;

    public static SongRepository LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("songs.json が見つかりません。", path);
        return LoadFromString(File.ReadAllText(path));
    }

    public static SongRepository LoadFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SongRepository(Array.Empty<SongRecord>());

        var dto = JsonSerializer.Deserialize<List<SongRecordDto>>(json, Options);
        if (dto is null) return new SongRepository(Array.Empty<SongRecord>());

        var records = new List<SongRecord>(dto.Count);
        foreach (var d in dto)
        {
            if (string.IsNullOrEmpty(d.Id) || string.IsNullOrEmpty(d.Title))
                continue;

            var normalized = d.TitleNormalized ?? TitleNormalizer.Normalize(d.Title);
            var charts = d.Charts ?? new Dictionary<string, int>();
            records.Add(new SongRecord(d.Id, d.Title, normalized, charts));
        }
        return new SongRepository(records);
    }

    private sealed class SongRecordDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }

        [JsonPropertyName("title_normalized")]
        public string? TitleNormalized { get; set; }

        public Dictionary<string, int>? Charts { get; set; }
    }
}
