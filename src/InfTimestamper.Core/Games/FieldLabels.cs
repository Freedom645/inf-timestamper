namespace InfTimestamper.Core.Games;

/// <summary>
/// 識別子キー（<see cref="FieldKeys"/>）に対応する日本語の論理名。
/// 設定画面の識別子セレクトボックスとサジェストで「論理名 ($key)」の形で見せるために持つ
/// （キーだけだとユーザに意味が伝わらないため）。
/// </summary>
public static class FieldLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FieldKeys.Timestamp] = "タイムスタンプ",
            [FieldKeys.Title] = "楽曲名",
            [FieldKeys.Level] = "レベル",
            [FieldKeys.DiffLong] = "難易度",
            [FieldKeys.DiffShort] = "難易度（略式表記）",
            [FieldKeys.ExScore] = "EX スコア",
            [FieldKeys.Score] = "スコア",
            [FieldKeys.MissCount] = "ミスカウント",
            [FieldKeys.DjLevel] = "DJ レベル",
            [FieldKeys.Lamp] = "クリアランプ",
            [FieldKeys.Rank] = "クリアランク",
            [FieldKeys.Medal] = "クリアメダル",
            [FieldKeys.Bad] = "BAD 数",
            [FieldKeys.Grade] = "グレード",
            [FieldKeys.ClearLamp] = "クリアランプ",
            [FieldKeys.ScoreShort] = "スコア（千点表記）",
        };

    /// <summary>論理名。未知のキーはキーそのものを返す（前方互換で増えた識別子でも表示が壊れないように）。</summary>
    public static string Of(string key)
        => key is not null && Labels.TryGetValue(key, out var label) ? label : key ?? string.Empty;
}

/// <summary>設定画面で識別子を選ばせるときの 1 項目。</summary>
public sealed record IdentifierChoice(string Key)
{
    /// <summary>フォーマット文字列に挿入される文字列。</summary>
    public string Token => "$" + Key;

    /// <summary>論理名。</summary>
    public string Label => FieldLabels.Of(Key);

    /// <summary>セレクトボックスに出す「論理名 ($key)」表記。</summary>
    public string Display => $"{Label} ({Token})";
}
