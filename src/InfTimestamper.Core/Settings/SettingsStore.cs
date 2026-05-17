using System.Text.Encodings.Web;
using System.Text.Json;

namespace InfTimestamper.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DefaultSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "inf-timestamper",
        "settings.json");

    public AppSettings Load(string path)
    {
        if (!File.Exists(path))
            return AppSettings.CreateDefault();

        try
        {
            using var fs = File.OpenRead(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(fs, Options);
            if (settings is null)
                return AppSettings.CreateDefault();

            FillMissingDefaults(settings);
            return settings;
        }
        catch (JsonException)
        {
            // 破損時はデフォルトを返す（既定ファイルは触らず、上書き保存は次回の Save まで保留）
            return AppSettings.CreateDefault();
        }
    }

    public void SaveAtomic(AppSettings settings, string path)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tmp = path + ".tmp";
        var bak = path + ".bak";

        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, settings, Options);
            fs.Flush(flushToDisk: true);
        }

        try
        {
            if (File.Exists(path))
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
            if (File.Exists(bak)) File.Delete(bak);
        }
        catch
        {
            try
            {
                if (!File.Exists(path) && File.Exists(bak))
                    File.Move(bak, path);
            }
            catch { /* リカバリも失敗 */ }
            throw;
        }
    }

    private static void FillMissingDefaults(AppSettings settings)
    {
        // 旧 schemaVersion 由来の欠落フィールドにデフォルト値を埋める
        settings.General ??= new GeneralSettings { BackupDirectory = AppSettings.DefaultBackupDirectory() };
        if (string.IsNullOrEmpty(settings.General.BackupDirectory))
            settings.General.BackupDirectory = AppSettings.DefaultBackupDirectory();

        settings.Obs ??= new ObsConnectionSettings
        {
            Host = AppSettings.DefaultObsHost,
            Port = AppSettings.DefaultObsPort,
        };

        settings.Infinitas ??= new InfinitasSettings
        {
            TimestampFormat = AppSettings.DefaultTimestampFormat,
            CaptureObs = new ObsConnectionSettings { Host = AppSettings.DefaultObsHost, Port = AppSettings.DefaultObsPort },
        };

        if (string.IsNullOrEmpty(settings.Infinitas.TimestampFormat))
            settings.Infinitas.TimestampFormat = AppSettings.DefaultTimestampFormat;

        settings.Infinitas.CaptureObs ??= new ObsConnectionSettings
        {
            Host = AppSettings.DefaultObsHost,
            Port = AppSettings.DefaultObsPort,
        };
    }
}
