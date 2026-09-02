using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Games;

/// <summary>
/// ゲームごとに異なる「表示名・有効な識別子・プレビュー用ダミーデータ・既定フォーマット」を集約する。
/// ゲームを増やすときに触る箇所をここ 1 つに閉じるのが狙い（Phase 9 で INFINITAS 専用設計から抽出）。
/// </summary>
public static class GameCatalog
{
    /// <summary>UI のゲーム選択に並べる順序。</summary>
    public static IReadOnlyList<GameId> AllGames { get; } = new[]
    {
        GameId.Infinitas,
        GameId.Popn,
        GameId.Sdvx,
    };

    private static readonly IReadOnlyList<string> InfinitasIdentifiers = new[]
    {
        FieldKeys.Timestamp,
        FieldKeys.Title,
        FieldKeys.DiffLong,
        FieldKeys.DiffShort,
        FieldKeys.Level,
        FieldKeys.MissCount,
        FieldKeys.ExScore,
        FieldKeys.DjLevel,
        FieldKeys.Lamp,
    };

    private static readonly IReadOnlyList<string> PopnIdentifiers = new[]
    {
        FieldKeys.Timestamp,
        FieldKeys.Title,
        FieldKeys.DiffLong,
        FieldKeys.DiffShort,
        FieldKeys.Level,
        FieldKeys.Rank,
        FieldKeys.Medal,
        FieldKeys.Score,
        FieldKeys.Bad,
    };

    private static readonly IReadOnlyList<string> SdvxIdentifiers = new[]
    {
        FieldKeys.Timestamp,
        FieldKeys.Title,
        FieldKeys.DiffLong,
        FieldKeys.DiffShort,
        FieldKeys.Level,
        FieldKeys.Grade,
        FieldKeys.ClearLamp,
        FieldKeys.Score,
        FieldKeys.ScoreShort,
        FieldKeys.ExScore,
    };

    private static readonly IReadOnlyList<IdentifierChoice> InfinitasChoices = ToChoices(InfinitasIdentifiers);
    private static readonly IReadOnlyList<IdentifierChoice> PopnChoices = ToChoices(PopnIdentifiers);
    private static readonly IReadOnlyList<IdentifierChoice> SdvxChoices = ToChoices(SdvxIdentifiers);

    private static IReadOnlyList<IdentifierChoice> ToChoices(IReadOnlyList<string> keys)
        => keys.Select(key => new IdentifierChoice(key)).ToArray();

    private static readonly IReadOnlyDictionary<string, string> InfinitasPreview =
        new Dictionary<string, string>
        {
            [FieldKeys.Timestamp] = "00:01:23",
            [FieldKeys.Title] = "Sample Song",
            [FieldKeys.DiffLong] = "ANOTHER",
            [FieldKeys.DiffShort] = "SPA",
            [FieldKeys.Level] = "11",
            [FieldKeys.MissCount] = "3",
            [FieldKeys.ExScore] = "1234",
            [FieldKeys.DjLevel] = "AAA",
            [FieldKeys.Lamp] = "FC",
        };

    private static readonly IReadOnlyDictionary<string, string> PopnPreview =
        new Dictionary<string, string>
        {
            [FieldKeys.Timestamp] = "00:01:23",
            [FieldKeys.Title] = "Sample Song",
            [FieldKeys.DiffLong] = "HYPER",
            [FieldKeys.DiffShort] = "H",
            [FieldKeys.Level] = "38",
            [FieldKeys.Rank] = "AA",
            [FieldKeys.Medal] = "銀星",
            [FieldKeys.Score] = "93578",
            [FieldKeys.Bad] = "2",
        };

    private static readonly IReadOnlyDictionary<string, string> SdvxPreview =
        new Dictionary<string, string>
        {
            [FieldKeys.Timestamp] = "00:01:23",
            [FieldKeys.Title] = "Sample Song",
            [FieldKeys.DiffLong] = "EXHAUST",
            [FieldKeys.DiffShort] = "EXH",
            [FieldKeys.Level] = "17",
            [FieldKeys.Grade] = "AA+",
            [FieldKeys.ClearLamp] = "UC",
            [FieldKeys.Score] = "9765432",
            [FieldKeys.ScoreShort] = "9765",
            [FieldKeys.ExScore] = "3210",
        };

    /// <summary>ゲーム選択 UI や状態表示に出す名称。</summary>
    public static string DisplayName(GameId game) => game switch
    {
        GameId.Infinitas => "beatmania IIDX INFINITAS",
        GameId.Popn => "pop'n music",
        GameId.Sdvx => "SOUND VOLTEX",
        _ => game.ToSerializedString(),
    };

    /// <summary>そのゲームで展開できる識別子（<c>$</c> 抜き）。設定画面のセレクトボックスの中身。</summary>
    public static IReadOnlyList<string> Identifiers(GameId game) => game switch
    {
        GameId.Popn => PopnIdentifiers,
        GameId.Sdvx => SdvxIdentifiers,
        _ => InfinitasIdentifiers,
    };

    /// <summary>設定画面のプレビュー用ダミーデータ（要件：ハードコードで表示する）。</summary>
    public static IReadOnlyDictionary<string, string> PreviewFields(GameId game) => game switch
    {
        GameId.Popn => PopnPreview,
        GameId.Sdvx => SdvxPreview,
        _ => InfinitasPreview,
    };

    /// <summary>
    /// 設定画面の識別子セレクトボックス / サジェストに出す項目。
    /// キーだけでは意味が伝わらないので、論理名を添えた <see cref="IdentifierChoice"/> で返す。
    /// </summary>
    public static IReadOnlyList<IdentifierChoice> IdentifierChoices(GameId game) => game switch
    {
        GameId.Popn => PopnChoices,
        GameId.Sdvx => SdvxChoices,
        _ => InfinitasChoices,
    };

    /// <summary>そのゲームのプレイ検知に使う外部ツール名（設定画面の説明文やログに使う）。</summary>
    public static string WatcherToolName(GameId game) => game switch
    {
        GameId.Popn => "popn-lively-tracker",
        GameId.Sdvx => "SDVX Helper",
        _ => "Reflux",
    };
}
