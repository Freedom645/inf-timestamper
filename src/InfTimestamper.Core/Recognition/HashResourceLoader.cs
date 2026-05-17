using System.Text.Json;
using System.Text.Json.Serialization;
using InfTimestamper.Core.Recognition.Json;

namespace InfTimestamper.Core.Recognition;

public static class HashResourceLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static HashResource Load(string path)
    {
        if (!File.Exists(path))
            return HashResource.Empty();

        var json = File.ReadAllText(path);
        return LoadFromString(json);
    }

    public static HashResource LoadFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return HashResource.Empty();

        var dto = JsonSerializer.Deserialize<HashResourceDto>(json, Options)
            ?? throw new InvalidDataException("hashes.json をパースできません。");

        return new HashResource
        {
            States = BuildStates(dto.States),
            Difficulty = BuildIcons(dto.Difficulty),
            DjLevel = BuildIcons(dto.DjLevel),
            Lamp = BuildIcons(dto.Lamp),
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StateHashEntry>> BuildStates(Dictionary<string, List<StateEntryDto>>? raw)
    {
        if (raw is null || raw.Count == 0)
            return new Dictionary<string, IReadOnlyList<StateHashEntry>>();

        var dict = new Dictionary<string, IReadOnlyList<StateHashEntry>>(raw.Count);
        foreach (var (state, entries) in raw)
        {
            var list = new List<StateHashEntry>(entries.Count);
            foreach (var e in entries)
            {
                list.Add(new StateHashEntry(
                    e.Name ?? throw new InvalidDataException("states エントリに name が欠けています。"),
                    e.Roi,
                    e.Ahash,
                    e.Threshold ?? HashResource.DefaultThreshold));
            }
            dict[state] = list;
        }
        return dict;
    }

    private static IReadOnlyList<IconHashEntry> BuildIcons(List<IconEntryDto>? raw)
    {
        if (raw is null || raw.Count == 0) return Array.Empty<IconHashEntry>();

        var list = new List<IconHashEntry>(raw.Count);
        foreach (var e in raw)
        {
            list.Add(new IconHashEntry(
                e.Value ?? throw new InvalidDataException("icon エントリに value が欠けています。"),
                e.Roi,
                e.Ahash,
                e.Threshold ?? HashResource.DefaultThreshold));
        }
        return list;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new RoiJsonConverter());
        options.Converters.Add(new HexUlongJsonConverter());
        return options;
    }

    private sealed class HashResourceDto
    {
        public Dictionary<string, List<StateEntryDto>>? States { get; set; }
        public List<IconEntryDto>? Difficulty { get; set; }

        [JsonPropertyName("dj_level")]
        public List<IconEntryDto>? DjLevel { get; set; }

        public List<IconEntryDto>? Lamp { get; set; }
    }

    private sealed class StateEntryDto
    {
        public string? Name { get; set; }
        public Roi Roi { get; set; } = new(0, 0, 0, 0);
        public ulong Ahash { get; set; }
        public int? Threshold { get; set; }
    }

    private sealed class IconEntryDto
    {
        public string? Value { get; set; }
        public Roi Roi { get; set; } = new(0, 0, 0, 0);
        public ulong Ahash { get; set; }
        public int? Threshold { get; set; }
    }
}
