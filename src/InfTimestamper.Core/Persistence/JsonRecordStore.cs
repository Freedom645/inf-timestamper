using System.Text.Json;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Persistence.Json;

namespace InfTimestamper.Core.Persistence;

public sealed class JsonRecordStore
{
    private readonly JsonSerializerOptions _options;

    public JsonRecordStore() : this(JsonOptionsFactory.CreateRecordOptions())
    {
    }

    public JsonRecordStore(JsonSerializerOptions options)
    {
        _options = options;
    }

    public static string GenerateFileName(GameId game, DateTimeOffset startedAt)
    {
        var gameStr = game.ToSerializedString();
        var stamp = startedAt.ToString("yyyyMMdd_HHmmss");
        return $"{gameStr}_{stamp}.json";
    }

    public StreamRecord Load(string path)
    {
        using var fs = File.OpenRead(path);
        StreamRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<StreamRecord>(fs, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"ファイルが破損しています: {path}", ex);
        }

        if (record is null)
            throw new InvalidDataException($"ファイルが空です: {path}");

        if (record.SchemaVersion > StreamRecord.CurrentSchemaVersion)
            throw new IncompatibleSchemaException(record.SchemaVersion, StreamRecord.CurrentSchemaVersion);

        return record;
    }

    public void SaveAtomic(StreamRecord record, string path)
    {
        record.UpdatedAt = DateTimeOffset.Now;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        // 1. tmp に書き込み → fsync
        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, record, _options);
            fs.Flush(flushToDisk: true);
        }

        try
        {
            // 2. 既存正本を .bak に退避
            if (File.Exists(path))
            {
                if (File.Exists(bakPath))
                    File.Delete(bakPath);
                File.Move(path, bakPath);
            }

            // 3. tmp を正本にリネーム
            File.Move(tmpPath, path);

            // 4. .bak 削除
            if (File.Exists(bakPath))
                File.Delete(bakPath);
        }
        catch
        {
            // 失敗時のリカバリ：tmp を残し、.bak から正本を戻す
            try
            {
                if (!File.Exists(path) && File.Exists(bakPath))
                    File.Move(bakPath, path);
            }
            catch
            {
                // リカバリも失敗。例外はそのまま投げる。
            }
            throw;
        }
    }

    public IEnumerable<UnfinishedRecord> FindUnfinished(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        var files = Directory.EnumerateFiles(directory, "*.json")
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.LastWriteTime);

        foreach (var fi in files)
        {
            StreamRecord? record = null;
            try
            {
                record = Load(fi.FullName);
            }
            catch
            {
                // 破損ファイル等は静かにスキップ
                continue;
            }

            if (record.Stream.EndedAt is null)
                yield return new UnfinishedRecord(fi.FullName, fi.LastWriteTime, record);
        }
    }
}
