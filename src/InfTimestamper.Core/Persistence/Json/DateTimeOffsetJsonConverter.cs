using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Persistence.Json;

/// <summary>
/// 記録ファイルの日時を ISO 8601 拡張形式 + オフセットの**秒精度**で読み書きする
/// （要件「日時フォーマット」。既定のシリアライザは 1 秒未満の端数まで書いてしまう）。
/// </summary>
public sealed class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public const string Format = "yyyy-MM-ddTHH:mm:sszzz";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw))
            return default;

        // 端数つきの値（旧バージョンが書いたファイル）も読めるようにする
        return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.TruncateToSecond().ToString(Format, CultureInfo.InvariantCulture));
}
