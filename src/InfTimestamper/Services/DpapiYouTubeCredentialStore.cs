using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using InfTimestamper.Core.YouTube;

namespace InfTimestamper.Services;

/// <summary>
/// YouTube のログイン情報を DPAPI（CurrentUser）で暗号化してファイルに置く。
/// 同じ Windows ユーザでしか復号できないので、settings.json と違い他人に渡しても読まれない。
/// </summary>
public sealed class DpapiYouTubeCredentialStore : IYouTubeCredentialStore
{
    /// <summary>暗号化の付加エントロピー。他アプリの DPAPI データと取り違えないための目印。</summary>
    private static readonly byte[] Entropy = "inf-timestamper/youtube"u8.ToArray();

    private readonly string _path;

    public DpapiYouTubeCredentialStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "inf-timestamper",
        "youtube_credential.bin");

    public YouTubeStoredCredential? Load()
    {
        if (!File.Exists(_path)) return null;

        var encrypted = File.ReadAllBytes(_path);
        var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<YouTubeStoredCredential>(json);
    }

    public void Save(YouTubeStoredCredential credential)
    {
        if (credential is null) throw new ArgumentNullException(nameof(credential));

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.SerializeToUtf8Bytes(credential);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

        var tmp = _path + ".tmp";
        File.WriteAllBytes(tmp, encrypted);
        File.Move(tmp, _path, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
