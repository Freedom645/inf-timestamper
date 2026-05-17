using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class HashMatcherTests
{
    private static readonly Roi DummyRoi = new(0, 0, 16, 16);

    [Fact]
    public void FindBestMatch_NearestWithinThreshold_ReturnsBest()
    {
        var matcher = new HashMatcher();
        var candidates = new[]
        {
            new HashCandidate("A", 0b0000UL, 5),
            new HashCandidate("B", 0b1100UL, 5),
            new HashCandidate("C", 0b1111UL, 5),
        };

        var result = matcher.FindBestMatch(0b0001UL, candidates);

        Assert.NotNull(result);
        Assert.Equal("A", result!.Name);
        Assert.Equal(1, result.Distance);
    }

    [Fact]
    public void FindBestMatch_AllExceedThreshold_ReturnsNull()
    {
        var matcher = new HashMatcher();
        var candidates = new[]
        {
            new HashCandidate("A", 0b1111UL, 1),
            new HashCandidate("B", 0b1100UL, 1),
        };

        var result = matcher.FindBestMatch(0b0000UL, candidates);
        Assert.Null(result);
    }

    [Fact]
    public void FindBestMatch_EmptyCandidates_ReturnsNull()
    {
        var matcher = new HashMatcher();
        Assert.Null(matcher.FindBestMatch(0UL, Array.Empty<HashCandidate>()));
    }

    [Fact]
    public void FindBestMatch_NullCandidates_ReturnsNull()
    {
        var matcher = new HashMatcher();
        Assert.Null(matcher.FindBestMatch(0UL, null!));
    }
}
