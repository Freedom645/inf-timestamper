namespace InfTimestamper.Core.Obs;

public static class BackoffSchedule
{
    public static TimeSpan GetDelay(int retryAttempt) => retryAttempt switch
    {
        <= 1 => TimeSpan.FromSeconds(1),
        2 => TimeSpan.FromSeconds(5),
        3 => TimeSpan.FromSeconds(10),
        4 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromSeconds(60),
    };
}
