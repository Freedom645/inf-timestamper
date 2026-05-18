namespace InfTimestamper.Core.Recognition;

public sealed record FrameRecognition(
    DateTimeOffset CapturedAt,
    RecognizedState State,
    HashMatchResult? StateMatch,
    IReadOnlyDictionary<string, string> Fields,
    PlaySide DetectedSide = PlaySide.Unknown);

public static class RecognitionFieldKeys
{
    public const string Title = "title";
    public const string DiffShort = "diff_s";
    public const string DiffLong = "diff_l";
    public const string Level = "level";
    public const string MissCount = "miss_count";
    public const string ExScore = "ex_score";
    public const string DjLevel = "dj_level";
    public const string Lamp = "lamp";
}

public static class RecognitionRoiKeys
{
    /// <summary>
    /// 難易度文字（NORMAL/HYPER/ANOTHER 等）の色判定用 ROI キー。
    /// 1P/2P 共通（リザルト画面の下部中央に表示される difficulty ラベル位置）。
    /// rois.json で `"difficulty_color": [x, y, w, h]` 形式で指定する。
    /// </summary>
    public const string DifficultyColor = "difficulty_color";

    /// <summary>
    /// FAILED 判定用の背景サンプル ROI キー。FAILED 時のみ画面全体が赤いオーバーレイになる
    /// 性質を利用し、Result 画面で lamp が HARD と判定されたあと、この ROI 内の赤ピクセル比率で
    /// HARD と FAILED を区別する。1P/2P 共通（背景は画面中央の広い領域でサンプル）。
    /// rois.json で `"failed_background": [x, y, w, h]` 形式で指定する。
    /// </summary>
    public const string FailedBackground = "failed_background";

    /// <summary>
    /// クリアランプの色判定用 ROI キー（1P サイド）。
    /// rois.json で `"lamp_color_1p": [x, y, w, h]` 形式で指定する。
    /// </summary>
    public const string LampColor1P = "lamp_color_1p";

    /// <summary>
    /// クリアランプの色判定用 ROI キー（2P サイド）。
    /// rois.json で `"lamp_color_2p": [x, y, w, h]` 形式で指定する。
    /// </summary>
    public const string LampColor2P = "lamp_color_2p";

    /// <summary>
    /// プレイサイドに応じた lamp_color の ROI キーを返す。
    /// Unknown のときは 2P 既定（INFINITAS のほとんどのプレイは 2P で配信されるため）。
    /// </summary>
    public static string LampColor(PlaySide side) => side switch
    {
        PlaySide.OneP => LampColor1P,
        _ => LampColor2P,
    };

    /// <summary>
    /// プレイサイド付きの ROI キーを組み立てる（例: "miss_count" + 1P → "miss_count_1p"）。
    /// Unknown のときは 2P 既定 suffix。
    /// </summary>
    public static string WithSide(string baseKey, PlaySide side)
    {
        var suffix = side switch
        {
            PlaySide.OneP => PlaySides.OnePSuffix,
            _ => PlaySides.TwoPSuffix,
        };
        return $"{baseKey}_{suffix}";
    }
}
