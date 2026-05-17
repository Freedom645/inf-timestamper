namespace InfTimestamper.Core.Obs;

public enum ObsConnectionManagerState
{
    Idle,
    Connecting,
    Connected,
    Reconnecting,
    Stopped,
}

public sealed class ObsConnectionManagerStateChangedEventArgs : EventArgs
{
    public ObsConnectionManagerStateChangedEventArgs(ObsConnectionManagerState state, int retryAttempt)
    {
        State = state;
        RetryAttempt = retryAttempt;
    }

    public ObsConnectionManagerState State { get; }
    public int RetryAttempt { get; }
}
