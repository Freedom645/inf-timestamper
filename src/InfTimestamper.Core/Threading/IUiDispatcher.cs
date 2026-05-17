namespace InfTimestamper.Core.Threading;

public interface IUiDispatcher
{
    void Invoke(Action action);
}

public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public static readonly ImmediateUiDispatcher Instance = new();
    public void Invoke(Action action) => action?.Invoke();
}
