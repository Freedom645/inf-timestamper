namespace InfTimestamper.Core.Updates;

public static class VersionComparer
{
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrEmpty(tag)) return false;

        var trimmed = tag.TrimStart('v', 'V').Trim();
        // セマンティックバージョンのプレリリース部分（-beta など）を除去
        var dash = trimmed.IndexOf('-');
        if (dash >= 0) trimmed = trimmed[..dash];

        return Version.TryParse(trimmed, out version!);
    }

    public static bool IsNewer(string? remoteTag, Version current)
    {
        if (!TryParseTag(remoteTag, out var remote)) return false;
        return Normalize(remote).CompareTo(Normalize(current)) > 0;
    }

    private static Version Normalize(Version v)
    {
        // Major.Minor.Build まで比較し、Build が -1 なら 0 として扱う
        return new Version(
            v.Major < 0 ? 0 : v.Major,
            v.Minor < 0 ? 0 : v.Minor,
            v.Build < 0 ? 0 : v.Build);
    }
}
