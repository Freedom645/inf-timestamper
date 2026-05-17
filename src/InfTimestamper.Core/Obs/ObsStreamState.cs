namespace InfTimestamper.Core.Obs;

public enum ObsStreamState
{
    Unknown,
    Starting,
    Started,
    Stopping,
    Stopped,
}

public sealed class ObsStreamStateChangedEventArgs : EventArgs
{
    public ObsStreamStateChangedEventArgs(ObsStreamState state, bool isActive)
    {
        State = state;
        IsActive = isActive;
    }

    public ObsStreamState State { get; }
    public bool IsActive { get; }
}
