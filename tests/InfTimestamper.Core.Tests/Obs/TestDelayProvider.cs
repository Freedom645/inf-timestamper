using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

internal sealed class TestDelayProvider : IDelayProvider
{
    private readonly List<TaskCompletionSource> _gates = new();

    public List<TimeSpan> RequestedDelays { get; } = new();

    public int PendingCount
    {
        get { lock (_gates) return _gates.Count; }
    }

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(duration);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gates) _gates.Add(tcs);

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    public bool ReleaseNext()
    {
        TaskCompletionSource? next;
        lock (_gates)
        {
            if (_gates.Count == 0) return false;
            next = _gates[0];
            _gates.RemoveAt(0);
        }
        return next.TrySetResult();
    }
}
