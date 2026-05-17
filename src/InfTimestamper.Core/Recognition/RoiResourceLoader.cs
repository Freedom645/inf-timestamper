using System.Text.Json;
using InfTimestamper.Core.Recognition.Json;

namespace InfTimestamper.Core.Recognition;

public static class RoiResourceLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static RoiResource Load(string path)
    {
        if (!File.Exists(path)) return RoiResource.Empty();
        return LoadFromString(File.ReadAllText(path));
    }

    public static RoiResource LoadFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return RoiResource.Empty();

        var raw = JsonSerializer.Deserialize<Dictionary<string, Roi>>(json, Options);
        if (raw is null || raw.Count == 0) return RoiResource.Empty();

        return new RoiResource { Rois = raw };
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new RoiJsonConverter());
        return options;
    }
}
