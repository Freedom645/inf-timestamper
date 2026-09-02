using System.Net;
using System.Net.Sockets;

namespace InfTimestamper.ViewModels.Settings;

/// <summary>
/// 接続先ホスト欄の妥当性判定。OBS 連携タブと SOUND VOLTEX タブで同じ規則を使う
/// （要件「IPアドレスv4形式又はチェックボックスでローカルホスト」）。
/// </summary>
public static class HostValidation
{
    public static bool IsIPv4OrLocalhost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out var address)) return false;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        // TryParse は "192.168.1" のような省略記法も通してしまうので、
        // 正規化した文字列と一致するかで 4 オクテット表記に限定する
        return string.Equals(address.ToString(), host, StringComparison.Ordinal);
    }

    public static bool IsLocalhost(string? host)
        => string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
           || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
}
