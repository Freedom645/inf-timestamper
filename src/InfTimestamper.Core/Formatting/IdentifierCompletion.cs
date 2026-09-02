using System.Text.RegularExpressions;
using InfTimestamper.Core.Games;

namespace InfTimestamper.Core.Formatting;

/// <summary>
/// タイムスタンプフォーマット入力欄の識別子サジェストのロジック。
///
/// WPF の <c>Behaviors/IdentifierSuggestion</c> は Popup とキー操作の面倒を見るだけで、
/// 「どこからどこまでが入力中の識別子か」「どの候補を出すか」「確定したらどう置き換えるか」は
/// ここに置いてテストできるようにしている。
/// </summary>
public static class IdentifierCompletion
{
    /// <summary>
    /// キャレット直前が「<c>$</c> + 識別子として成立する文字」かを判定する。
    /// 字種は <see cref="FormatExpander"/> の展開パターンと揃えてある。
    /// </summary>
    private static readonly Regex TokenPattern = new(@"\$([a-z0-9_]*)$", RegexOptions.Compiled);

    /// <summary>
    /// <paramref name="caret"/> の直前にある入力中の識別子トークンを取り出す。
    /// </summary>
    /// <param name="tokenStart"><c>$</c> の位置（置換の開始位置）。</param>
    /// <param name="prefix"><c>$</c> に続けて入力済みの文字列（絞り込みに使う。<c>$</c> 直後なら空）。</param>
    public static bool TryGetToken(string? text, int caret, out int tokenStart, out string prefix)
    {
        tokenStart = -1;
        prefix = string.Empty;

        var source = text ?? string.Empty;
        if (caret < 0 || caret > source.Length) return false;

        var match = TokenPattern.Match(source[..caret]);
        if (!match.Success) return false;

        tokenStart = match.Index;
        prefix = match.Groups[1].Value;
        return true;
    }

    /// <summary>入力済みの <paramref name="prefix"/> で候補を前方一致で絞り込む。</summary>
    public static IReadOnlyList<IdentifierChoice> Filter(
        IReadOnlyList<IdentifierChoice>? candidates, string? prefix)
    {
        if (candidates is null || candidates.Count == 0) return Array.Empty<IdentifierChoice>();
        if (string.IsNullOrEmpty(prefix)) return candidates;

        return candidates
            .Where(choice => choice.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>
    /// 入力中のトークン（<paramref name="tokenStart"/>〜<paramref name="caret"/>）を
    /// <c>$<paramref name="key"/></c> で置き換えた結果と、置換後のキャレット位置を返す。
    /// </summary>
    public static (string Text, int Caret) Apply(string? text, int tokenStart, int caret, string key)
    {
        var source = text ?? string.Empty;
        var end = Math.Clamp(caret, 0, source.Length);
        var start = Math.Clamp(tokenStart, 0, end);

        var replacement = "$" + (key ?? string.Empty);
        return (source[..start] + replacement + source[end..], start + replacement.Length);
    }
}
