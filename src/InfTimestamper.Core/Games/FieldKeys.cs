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

    /// <summary>譜面の難易度（正式名）。INFINITAS: BEGINNER〜LEGGENDARIA / pop'n: EASY〜EX。</summary>
    public const string DiffLong = "diff_l";

    /// <summary>譜面の難易度（略式表記）。INFINITAS: {SP,DP}{B,N,H,A,L} / pop'n: {E,N,H,EX}。</summary>
    public const string DiffShort = "diff_s";

    // ---- INFINITAS 固有 ----

    /// <summary>ミスカウント（BAD + POOR）。</summary>
    public const string MissCount = "miss_count";

    /// <summary>EX スコア。</summary>
    public const string ExScore = "ex_score";

    /// <summary>DJ レベル（AAA〜F）。</summary>
    public const string DjLevel = "dj_level";

    /// <summary>クリアランプ（FAILED / A-EASY / EASY / NORMAL / HARD / EX-HARD / FC）。</summary>
    public const string Lamp = "lamp";

    // ---- pop'n music 固有 ----

    /// <summary>クリアランク（S / AAA / AA / A / B / C / D / E）。INFINITAS の DJ レベルとは尺度が違う。</summary>
    public const string Rank = "rank";

    /// <summary>クリアメダル（青丸〜金星）。INFINITAS のクリアランプとは体系が違う。</summary>
    public const string Medal = "medal";

    /// <summary>スコア（0〜100000）。INFINITAS の EX スコアとは尺度が違う。</summary>
    public const string Score = "score";

    /// <summary>BAD 数。pop'n はコンボが切れるのが BAD のみなので、INFINITAS のミスカウントとは意味が違う。</summary>
    public const string Bad = "bad";
}
