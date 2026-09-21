namespace InfTimestamper.Core.Updates;

/// <summary>GitHub Releases のタグ（<c>v1.2.0</c> / <c>v1.2.0-alpha.1</c>）と実行中バージョンの比較。</summary>
public static class VersionComparer
{
    /// <summary>タグを <see cref="SemanticVersion"/> として解釈する。先頭の <c>v</c> は許容する。</summary>
    public static bool TryParseTag(string? tag, out SemanticVersion version)
        => SemanticVersion.TryParse(tag, out version);

    /// <summary>
    /// <paramref name="remoteTag"/> が <paramref name="current"/> より新しいか。
    /// プレリリースは SemVer の規則で比較する（<c>1.2.0-alpha.1</c> &lt; <c>1.2.0</c>）ので、
    /// α 版を使っている人には正式版が「新しい」と判定される。
    /// </summary>
    public static bool IsNewer(string? remoteTag, SemanticVersion current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!TryParseTag(remoteTag, out var remote)) return false;
        return remote.CompareTo(current) > 0;
    }
}
