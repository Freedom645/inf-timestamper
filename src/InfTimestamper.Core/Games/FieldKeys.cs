namespace InfTimestamper.Core.Games;

/// <summary>
/// タイムスタンプ識別子のキー（フォーマット文字列の <c>$xxx</c> から <c>$</c> を除いたもの。
/// 永続化 JSON の <c>timestamps[].fields</c> のキーでもある）。
///
/// 方針（Phase 9 で決定）：**ゲーム間で意味が変わらない識別子だけを共通で流用し、
/// 成績系のようにゲームごとに意味が異なるものはゲーム固有の名前を新設する**。
/// どの識別子がどのゲームで有効かは <see cref="GameCatalog.Identifiers"/> が持つ。
/// </summary>
public static class FieldKeys
{
    // ---- 全ゲーム共通 ----

    /// <summary>配信開始からの相対時刻（hh:mm:ss）。エントリの fields ではなく表示時に合成される。</summary>
    public const string Timestamp = "timestamp";

    /// <summary>楽曲名。</summary>
    public const string Title = "title";

    /// <summary>譜面のレベル。</summary>
    public const string Level = "level";

    /// <summary>
    /// 譜面の難易度（正式名）。INFINITAS: BEGINNER〜LEGGENDARIA / pop'n: EASY〜EX /
    /// SDVX: NOVICE〜MAXIMUM。
    /// </summary>
    public const string DiffLong = "diff_l";

    /// <summary>
    /// 譜面の難易度（略式表記）。INFINITAS: {SP,DP}{B,N,H,A,L} / pop'n: {E,N,H,EX} /
    /// SDVX: {NOV,ADV,EXH,MXM}。
    /// </summary>
    public const string DiffShort = "diff_s";

    // ---- 複数ゲームで流用する成績系 ----
    // 「そのプレイの得点」という意味はゲーム間で変わらないので流用する（尺度の違いは
    // レベルが INFINITAS 1〜12 / pop'n 1〜50 で違うのと同じ扱い）。

    /// <summary>EX スコア。INFINITAS（0〜約 4000）と SDVX（0〜ノーツ数×5）で有効。</summary>
    public const string ExScore = "ex_score";

    /// <summary>スコア。pop'n（0〜100000）と SDVX（0〜10,000,000）で有効。</summary>
    public const string Score = "score";

    // ---- INFINITAS 固有 ----

    /// <summary>ミスカウント（BAD + POOR）。</summary>
    public const string MissCount = "miss_count";

    /// <summary>DJ レベル（AAA〜F）。</summary>
    public const string DjLevel = "dj_level";

    /// <summary>クリアランプ（FAILED / A-EASY / EASY / NORMAL / HARD / EX-HARD / FC）。</summary>
    public const string Lamp = "lamp";

    // ---- pop'n music 固有 ----

    /// <summary>クリアランク（S / AAA / AA / A / B / C / D / E）。INFINITAS の DJ レベルとは尺度が違う。</summary>
    public const string Rank = "rank";

    /// <summary>クリアメダル（青丸〜金星）。INFINITAS のクリアランプとは体系が違う。</summary>
    public const string Medal = "medal";

    /// <summary>BAD 数。pop'n はコンボが切れるのが BAD のみなので、INFINITAS のミスカウントとは意味が違う。</summary>
    public const string Bad = "bad";

    // ---- SOUND VOLTEX 固有 ----

    /// <summary>
    /// クリアランプ（PLAYED / COMP / EXC-COMP / MAXXIVE / UC / PUC）。
    /// INFINITAS の <see cref="Lamp"/> とは体系が違うため別キーにしている。
    /// </summary>
    public const string ClearLamp = "clear_lamp";

    /// <summary>グレード（S / AAA+ / AAA / AA+ / AA / A+ / A / B / C / D）。</summary>
    public const string Grade = "grade";

    /// <summary>
    /// スコアの千点表記（<see cref="Score"/> を 1000 で割った整数）。
    /// SDVX は 0〜10,000,000 のスコアを「9,850k」のように読むことが多いため用意している。
    /// </summary>
    public const string ScoreShort = "score_short";

    // ---- 型情報 ----

    /// <summary>
    /// 値を数値として保存する識別子（要件「数値型フィールドは数値のまま保存し、フォーマット展開時に文字列化する」）。
    /// ここに無いキーは文字列として保存する。
    /// </summary>
    private static readonly HashSet<string> Numeric = new(StringComparer.Ordinal)
    {
        Level, MissCount, ExScore, Score, Bad, ScoreShort,
    };

    /// <summary>そのキーの値を JSON に数値で書くか。</summary>
    public static bool IsNumeric(string key) => key is not null && Numeric.Contains(key);
}
