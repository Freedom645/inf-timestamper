using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfTimestamper.Core.Persistence.Json;

public static class JsonOptionsFactory
{
    public static JsonSerializerOptions CreateRecordOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };
        options.Converters.Add(new UlidJsonConverter());
        options.Converters.Add(new GameIdJsonConverter());
        return options;
    }
}
