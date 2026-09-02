using System.Text;
using System.Text.Json;

namespace InfTimestamper.Core.Tests.TestHelpers;

/// <summary>
/// SDVX Helper のデータ配信 WebSocket が流すメッセージを組み立てるテスト用ヘルパー。
/// キー名・値の型は SDVX Helper 側の <c>_broadcast_nowplaying</c> /
/// <c>get_today_results_data</c> に合わせている。
/// </summary>
public static class SdvxMessages
{
    /// <summary>曲決定画面（楽曲情報画面）由来の nowplaying。切り出し画像が一式そろう。</summary>
    public static string NowPlayingFromSongDecided(
        string title = "Sample Song",
        string difficulty = "EXH",
        object? level = null)
        => NowPlaying(title, difficulty, level ?? 17, withDetectImages: true);

    /// <summary>選曲画面でカーソルが動いたときの nowplaying。ジャケット以外の画像は空文字列。</summary>
    public static string NowPlayingFromSelect(
        string title = "Sample Song",
        string difficulty = "EXH",
        object? level = null)
        => NowPlaying(title, difficulty, level ?? 17, withDetectImages: false);

    private static string NowPlaying(string? title, string? difficulty, object? level, bool withDetectImages)
    {
        const string dataUrl = "data:image/png;base64,iVBORw0KGgo=";
        var detect = withDetectImages ? dataUrl : string.Empty;

        return JsonSerializer.Serialize(new
        {
            type = "nowplaying",
            data = new
            {
                title,
                difficulty,
                level,
                artist = "Sample Artist",
                bpm = "180",
                images = new
                {
                    jacket = dataUrl,
                    title = detect,
                    level = detect,
                    bpm = detect,
                    effector = detect,
                    illustrator = detect,
                },
            },
        });
    }

    /// <summary>today_results。<paramref name="items"/> は <see cref="ResultItem"/> の列。</summary>
    public static string TodayResults(params object[] items)
        => JsonSerializer.Serialize(new { type = "today_results", data = new { items } });

    /// <summary>today_results の 1 エントリ。</summary>
    public static object ResultItem(
        DateTimeOffset timestamp,
        string title = "Sample Song",
        string difficulty = "EXH",
        string lv = "17",
        int score = 9_765_432,
        int exscore = 3210,
        string grade = "AA+",
        int lamp = 5)
        => new
        {
            chart_id = $"{title}_{difficulty}",
            title,
            difficulty,
            lv,
            score,
            exscore,
            grade,
            lamp,
            vf = 3950,
            timestamp = timestamp.ToUnixTimeSeconds(),
        };

    public static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    /// <summary>メッセージ本文（<c>data</c>）を <see cref="JsonDocument"/> として取り出す。</summary>
    public static JsonDocument DataOf(string message)
    {
        using var document = JsonDocument.Parse(message);
        return JsonDocument.Parse(document.RootElement.GetProperty("data").GetRawText());
    }
}
