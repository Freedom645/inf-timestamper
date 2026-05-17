namespace InfTimestamper.Core.Recognition;

public sealed class HashMatcher : IHashMatcher
{
    public HashMatchResult? FindBestMatch(ulong observedHash, IEnumerable<HashCandidate> candidates)
    {
        if (candidates is null) return null;

        HashMatchResult? best = null;
        foreach (var candidate in candidates)
        {
            var distance = ImageHasher.HammingDistance(observedHash, candidate.Hash);
            if (distance > candidate.Threshold) continue;
            if (best is null || distance < best.Distance)
                best = new HashMatchResult(candidate.Name, distance);
        }
        return best;
    }
}
