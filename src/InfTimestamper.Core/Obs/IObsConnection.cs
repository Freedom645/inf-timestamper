namespace InfTimestamper.Core.Obs;

public interface IObsConnection : IAsyncDisposable
{
    bool IsConnected { get; }

    event EventHandler? Connected;
    event EventHandler<ObsDisconnectedEventArgs>? Disconnected;
    event EventHandler<ObsStreamStateChangedEventArgs>? StreamStateChanged;

    Task ConnectAsync(ObsConnectionOptions options, TimeSpan timeout, CancellationToken cancellationToken);

    Task DisconnectAsync();

    Task<ObsScreenshot> GetScreenshotAsync(string sourceName, CancellationToken cancellationToken);

    Task<bool> IsStreamActiveAsync(CancellationToken cancellationToken);
}

public sealed class ObsDisconnectedEventArgs : EventArgs
{
    public ObsDisconnectedEventArgs(string? reason)
    {
        Reason = reason;
    }

    public string? Reason { get; }
}
