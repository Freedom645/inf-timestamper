using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

internal sealed class FakeObsConnection : IObsConnection
{
    public bool IsConnected { get; private set; }

    public event EventHandler? Connected;
    public event EventHandler<ObsDisconnectedEventArgs>? Disconnected;
    public event EventHandler<ObsStreamStateChangedEventArgs>? StreamStateChanged;

    public Func<ObsConnectionOptions, Task>? ConnectHandler { get; set; }
    public Func<string, Task<ObsScreenshot>>? ScreenshotHandler { get; set; }
    public Func<Task<bool>>? StreamActiveHandler { get; set; }
    public Func<Task<ObsServerInfo>>? ServerInfoHandler { get; set; }
    public Func<Task<IReadOnlyList<string>>>? InputNamesHandler { get; set; }

    public int ConnectAttempts { get; private set; }
    public int DisposeCount { get; private set; }

    public async Task ConnectAsync(ObsConnectionOptions options, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ConnectAttempts++;
        if (ConnectHandler is not null)
            await ConnectHandler(options).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = true;
        Connected?.Invoke(this, EventArgs.Empty);
    }

    public Task DisconnectAsync()
    {
        SimulateDisconnect();
        return Task.CompletedTask;
    }

    public Task<ObsScreenshot> GetScreenshotAsync(string sourceName, CancellationToken cancellationToken)
        => ScreenshotHandler is null
            ? throw new InvalidOperationException("ScreenshotHandler 未設定")
            : ScreenshotHandler(sourceName);

    public Task<bool> IsStreamActiveAsync(CancellationToken cancellationToken)
        => StreamActiveHandler is null
            ? Task.FromResult(false)
            : StreamActiveHandler();

    public Task<ObsServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
        => ServerInfoHandler is null
            ? Task.FromResult(new ObsServerInfo("test-version", "scene"))
            : ServerInfoHandler();

    public Task<IReadOnlyList<string>> GetInputNamesAsync(CancellationToken cancellationToken)
        => InputNamesHandler is null
            ? Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>())
            : InputNamesHandler();

    public void SimulateDisconnect(string? reason = null)
    {
        if (!IsConnected) return;
        IsConnected = false;
        Disconnected?.Invoke(this, new ObsDisconnectedEventArgs(reason));
    }

    public void RaiseStreamStateChanged(ObsStreamState state, bool isActive)
        => StreamStateChanged?.Invoke(this, new ObsStreamStateChangedEventArgs(state, isActive));

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
