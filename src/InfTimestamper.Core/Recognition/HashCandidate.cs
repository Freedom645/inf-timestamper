namespace InfTimestamper.Core.Recognition;

public sealed record HashCandidate(string Name, ulong Hash, int Threshold);

public sealed record HashMatchResult(string Name, int Distance);
