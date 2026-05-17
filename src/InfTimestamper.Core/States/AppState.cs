namespace InfTimestamper.Core.States;

public enum AppState
{
    Initial,
    WaitingForStream,
    Recording,
    RecordingEnded,
}

public enum RecordingSubState
{
    SongSelect,
    PlayStart,
    Result,
}
