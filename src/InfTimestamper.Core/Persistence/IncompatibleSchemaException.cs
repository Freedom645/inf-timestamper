namespace InfTimestamper.Core.Persistence;

public sealed class IncompatibleSchemaException : Exception
{
    public int FileSchemaVersion { get; }
    public int SupportedSchemaVersion { get; }

    public IncompatibleSchemaException(int fileSchemaVersion, int supportedSchemaVersion)
        : base($"より新しいバージョンで作成されたファイルです (schemaVersion={fileSchemaVersion}, 本アプリの対応={supportedSchemaVersion})")
    {
        FileSchemaVersion = fileSchemaVersion;
        SupportedSchemaVersion = supportedSchemaVersion;
    }
}
