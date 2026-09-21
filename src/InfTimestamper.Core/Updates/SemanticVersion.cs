using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace InfTimestamper.Core.Updates;

/// <summary>
/// セマンティックバージョン（<c>Major.Minor.Patch[-prerelease]</c>）。
///
/// α 版（<c>1.2.0-alpha.1</c>）のようなプレリリースを扱うために持つ。
/// <see cref="System.Version"/> はプレリリース部分を表現できず、<c>1.2.0-alpha.1</c> と <c>1.2.0</c> を
/// 同じ値として扱ってしまうため、α 版利用者が正式版へ更新できない。
/// 比較規則は SemVer 2.0（プレリリース無し &gt; あり、識別子は数値なら数値比較、それ以外は辞書順）。
/// </summary>
public sealed record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease = null)
    : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled);

    public static SemanticVersion Zero { get; } = new(0, 0, 0);

    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    /// <summary>
    /// <c>v1.2.0-alpha.1</c> / <c>1.2.0</c> / <c>1.2.0-alpha.1+abc123</c> を解釈する。
    /// 先頭の <c>v</c> と末尾のビルドメタデータ（<c>+…</c>）は無視する。
    /// </summary>
    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = Pattern.Match(text.Trim().TrimStart('v', 'V'));
        if (!match.Success) return false;

        if (!int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        var patch = 0;
        if (match.Groups["patch"].Success
            && !int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out patch))
        {
            return false;
        }

        var pre = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
        version = new SemanticVersion(major, minor, patch, pre);
        return true;
    }

    /// <summary>
    /// アセンブリのバージョンを読む。<c>AssemblyInformationalVersion</c>（csproj の <c>&lt;Version&gt;</c> 由来で
    /// プレリリース表記を含む）を優先し、無ければ <c>AssemblyVersion</c> を使う。
    /// </summary>
    public static SemanticVersion FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (TryParse(informational, out var parsed)) return parsed;

        var numeric = assembly.GetName().Version;
        return numeric is null
            ? Zero
            : new SemanticVersion(Math.Max(numeric.Major, 0), Math.Max(numeric.Minor, 0), Math.Max(numeric.Build, 0));
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;

        var byNumber = Major.CompareTo(other.Major);
        if (byNumber == 0) byNumber = Minor.CompareTo(other.Minor);
        if (byNumber == 0) byNumber = Patch.CompareTo(other.Patch);
        if (byNumber != 0) return byNumber;

        // 数値部分が同じなら、プレリリース無し（正式版）の方が新しい
        if (!IsPrerelease) return other.IsPrerelease ? 1 : 0;
        if (!other.IsPrerelease) return -1;

        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    private static int ComparePrerelease(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        var count = Math.Min(a.Length, b.Length);

        for (var i = 0; i < count; i++)
        {
            var aNumeric = int.TryParse(a[i], NumberStyles.None, CultureInfo.InvariantCulture, out var an);
            var bNumeric = int.TryParse(b[i], NumberStyles.None, CultureInfo.InvariantCulture, out var bn);

            int result;
            if (aNumeric && bNumeric) result = an.CompareTo(bn);
            else if (aNumeric) result = -1;          // 数値識別子は英数字識別子より小さい
            else if (bNumeric) result = 1;
            else result = string.CompareOrdinal(a[i], b[i]);

            if (result != 0) return result;
        }

        // 先頭が全部同じなら識別子が多い方が新しい（alpha < alpha.1）
        return a.Length.CompareTo(b.Length);
    }

    public override string ToString()
        => IsPrerelease
            ? $"{Major}.{Minor}.{Patch}-{Prerelease}"
            : $"{Major}.{Minor}.{Patch}";
}
