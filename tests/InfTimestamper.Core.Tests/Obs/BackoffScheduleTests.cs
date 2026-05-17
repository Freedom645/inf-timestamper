using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

public class BackoffScheduleTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    [InlineData(10, 60)]
    [InlineData(100, 60)]
    public void GetDelay_ReturnsRequiredSchedule(int attempt, int expectedSeconds)
    {
        var delay = BackoffSchedule.GetDelay(attempt);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
