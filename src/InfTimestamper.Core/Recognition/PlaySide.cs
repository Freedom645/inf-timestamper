namespace InfTimestamper.Core.Recognition;

/// <summary>
/// IIDX のプレイサイド。1P (左) / 2P (右) でウィジェットの位置と ROI が変わる。
/// </summary>
public enum PlaySide
{
    Unknown,
    OneP,
    TwoP,
}

public static class PlaySides
{
    public const string OnePSuffix = "1p";
    public const string TwoPSuffix = "2p";

    /// <summary>
    /// state hash 名（例: "song_select/1p_arrow_center"）からプレイサイドを抽出する。
    /// 名前に "/1p_" / "/2p_" が含まれるかで判定。case-insensitive。
    /// </summary>
    public static PlaySide FromStateName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return PlaySide.Unknown;
        var lower = name.ToLowerInvariant();
        if (lower.Contains("/1p_") || lower.Contains("/1p/") || lower.StartsWith("1p_"))
            return PlaySide.OneP;
        if (lower.Contains("/2p_") || lower.Contains("/2p/") || lower.StartsWith("2p_"))
            return PlaySide.TwoP;
        return PlaySide.Unknown;
    }

    /// <summary>
    /// プレイサイドの ROI キー suffix を返す（例: "lamp_color_1p"）。
    /// </summary>
    public static string Suffix(PlaySide side) => side switch
    {
        PlaySide.OneP => OnePSuffix,
        PlaySide.TwoP => TwoPSuffix,
        _ => string.Empty,
    };
}
