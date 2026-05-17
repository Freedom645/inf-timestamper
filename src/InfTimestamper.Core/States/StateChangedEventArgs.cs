namespace InfTimestamper.Core.States;

public sealed class StateChangedEventArgs : EventArgs
{
    public AppState OldState { get; }
    public AppState NewState { get; }

    public StateChangedEventArgs(AppState oldState, AppState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

public sealed class SubStateChangedEventArgs : EventArgs
{
    public RecordingSubState? OldSubState { get; }
    public RecordingSubState? NewSubState { get; }

    public SubStateChangedEventArgs(RecordingSubState? oldSubState, RecordingSubState? newSubState)
    {
        OldSubState = oldSubState;
        NewSubState = newSubState;
    }
}

public sealed class InvalidStateTransitionException : InvalidOperationException
{
    public InvalidStateTransitionException(AppState currentState, string operation)
        : base($"状態 {currentState} で操作 '{operation}' は実行できません")
    {
    }
}
