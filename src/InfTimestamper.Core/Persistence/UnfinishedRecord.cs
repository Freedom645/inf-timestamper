using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Persistence;

public sealed record UnfinishedRecord(string FilePath, DateTime LastModified, StreamRecord Record);
