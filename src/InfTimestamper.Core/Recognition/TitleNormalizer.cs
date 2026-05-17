using System.Text;

namespace InfTimestamper.Core.Recognition;

public static class TitleNormalizer
{
    public static string Normalize(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        var upper = title.ToUpperInvariant();
        var sb = new StringBuilder(upper.Length);
        foreach (var ch in upper)
        {
            var code = (int)ch;

            // 全角英数 (U+FF01-U+FF5E) を半角に正規化
            if (code >= 0xFF01 && code <= 0xFF5E)
                code -= 0xFEE0;

            // 英数字 / ひらがな / カタカナ / CJK のみ残し、空白・記号を除去
            var isAscii  = (code >= 0x30 && code <= 0x39) || (code >= 0x41 && code <= 0x5A);
            var isKana   = code >= 0x3041 && code <= 0x30FA;
            var isCjk    = code >= 0x4E00 && code <= 0x9FFF;
            var isExtKa  = code >= 0x31F0 && code <= 0x31FF;

            if (isAscii || isKana || isCjk || isExtKa)
                sb.Append((char)code);
        }
        return sb.ToString();
    }
}
