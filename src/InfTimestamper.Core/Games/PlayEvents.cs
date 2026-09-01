namespace InfTimestamper.Core.Games;

/// <summary>プレイ開始の通知。<see cref="CapturedAt"/> が新規タイムスタンプエントリの playStartedAt になる。</summary>
public sealed class PlayStartedEventArgs : EventArgs
{
    public PlayStartedEventArgs(DateTimeOffset capturedAt, IReadOnlyDictionary<string, string> fields)
    {
        CapturedAt = capturedAt;
        Fields = fields;
    }

    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}

/// <summary>プレイリザルトの通知。<see cref="Fields"/> は直近のプレイ開始エントリにマージされる。</summary>
public sealed class PlayResultEventArgs : EventArgs
{
    public PlayResultEventArgs(DateTimeOffset capturedAt, IReadOnlyDictionary<string, string> fields)
    {
        CapturedAt = capturedAt;
        Fields = fields;
    }

    public DateTimeOffset CapturedAt { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}
