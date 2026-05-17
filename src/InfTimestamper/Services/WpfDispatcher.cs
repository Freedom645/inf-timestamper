using System.Windows;
using InfTimestamper.Core.Threading;

namespace InfTimestamper.Services;

public sealed class WpfDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        if (action is null) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
