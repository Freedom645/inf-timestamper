namespace InfTimestamper.Core.Obs;

public sealed record ObsConnectionOptions(string Host, int Port, string? Password)
{
    public string ToWebSocketUrl() => $"ws://{Host}:{Port}";
}
