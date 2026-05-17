namespace InfTimestamper.Core.Recognition;

public interface IHashMatcher
{
    HashMatchResult? FindBestMatch(ulong observedHash, IEnumerable<HashCandidate> candidates);
}
