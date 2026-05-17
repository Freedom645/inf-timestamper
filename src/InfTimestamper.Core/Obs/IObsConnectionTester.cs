namespace InfTimestamper.Core.Obs;

public interface IObsConnectionTester
{
    Task<ObsConnectionTestResult> TestAsync(ObsConnectionOptions options, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> FetchSourceNamesAsync(ObsConnectionOptions options, CancellationToken cancellationToken);
}

public sealed record ObsConnectionTestResult(bool Success, string Message, string? ObsVersion, string? CurrentScene);
