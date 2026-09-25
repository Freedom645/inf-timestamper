using System.Text;

namespace InfTimestamper.Core.YouTube;

/// <summary>
/// 概要欄の末尾にタイムスタンプのブロックを書き込む。
/// ブロックは「見出し行 + タイムスタンプ行」で、見出し行から概要欄の末尾までをアプリの管理範囲とする
/// （手書きの説明文は見出し行より前に残る）。見出し行が無ければ末尾に追記する。
/// </summary>
public static class YouTubeDescriptionComposer
{
    /// <summary>YouTube の概要欄の上限（バイト数。UTF-8 で数える）。</summary>
    public const int MaxDescriptionBytes = 5000;

    /// <summary>
    /// 既存の概要欄にタイムスタンプのブロックを差し込んだ文字列を返す。
    /// 上限を超える場合はタイムスタンプ行を末尾から落とす。
    /// </summary>
    /// <param name="existing">現在の概要欄。</param>
    /// <param name="heading">ブロックの見出し行（管理範囲の目印）。</param>
    /// <param name="timestampLines">タイムスタンプの各行。</param>
    /// <param name="droppedLines">上限超過で落とした行数。</param>
    public static string Compose(
        string? existing,
        string heading,
        IReadOnlyList<string> timestampLines,
        out int droppedLines)
    {
        if (string.IsNullOrWhiteSpace(heading))
            throw new ArgumentException("見出し行が空です。", nameof(heading));

        heading = Sanitize(heading.Trim());
        var prefix = StripManagedBlock(Normalize(existing), heading).TrimEnd();
        var lines = timestampLines.Select(l => Sanitize(l.TrimEnd())).ToList();

        droppedLines = 0;
        while (true)
        {
            var composed = Build(prefix, heading, lines);
            if (Encoding.UTF8.GetByteCount(composed) <= MaxDescriptionBytes || lines.Count == 0)
                return composed;
            lines.RemoveAt(lines.Count - 1);
            droppedLines++;
        }
    }

    /// <summary>
    /// 見出し行以降（アプリの管理範囲）を取り除いた概要欄。見出し行は最後に現れたものを採る
    /// （手書きの説明文中に同じ文言があっても、末尾側のブロックだけを置き換える）。
    /// </summary>
    internal static string StripManagedBlock(string description, string heading)
    {
        var lines = description.Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == heading)
                return string.Join('\n', lines.Take(i));
        }
        return description;
    }

    /// <summary>
    /// YouTube の概要欄では <c>&lt;</c> <c>&gt;</c> が使えない（API が invalidDescription で弾く）ので全角へ寄せる。
    /// </summary>
    public static string Sanitize(string text)
        => text.Replace('<', '＜').Replace('>', '＞');

    private static string Normalize(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

    private static string Build(string prefix, string heading, IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        if (prefix.Length > 0)
        {
            sb.Append(prefix);
            sb.Append("\n\n");
        }
        sb.Append(heading);
        foreach (var line in lines)
        {
            sb.Append('\n');
            sb.Append(line);
        }
        return sb.ToString();
    }
}
