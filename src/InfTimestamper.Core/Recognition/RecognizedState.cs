namespace InfTimestamper.Core.Recognition;

public enum RecognizedState
{
    Unknown,
    SongSelect,
    PlayStart,
    Result,
}

public static class RecognizedStateNames
{
    public const string SongSelect = "song_select";
    public const string PlayStart = "play_start";
    public const string Result = "result";

    public static RecognizedState FromString(string? raw) => raw switch
    {
        SongSelect => RecognizedState.SongSelect,
        PlayStart => RecognizedState.PlayStart,
        Result => RecognizedState.Result,
        _ => RecognizedState.Unknown,
    };
}
