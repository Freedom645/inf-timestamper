namespace InfTimestamper.Core.Obs;

public interface IDelayProvider
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

internal sealed class SystemDelayProvider : IDelayProvider
{
    public static readonly SystemDelayProvider Instance = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        => Task.Delay(duration, cancellationToken);
}
