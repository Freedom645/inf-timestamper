using System.Globalization;
using System.Windows.Input;

namespace InfTimestamper.ViewModels;

public sealed class ShiftCommand : ICommand
{
    private readonly Action<TimeSpan> _execute;

    public ShiftCommand(Action<TimeSpan> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => TryParseTimeSpan(parameter, out _);

    public void Execute(object? parameter)
    {
        if (TryParseTimeSpan(parameter, out var span))
            _execute(span);
    }

    private static bool TryParseTimeSpan(object? parameter, out TimeSpan span)
    {
        switch (parameter)
        {
            case TimeSpan t:
                span = t;
                return true;
            case string s when TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var parsed):
                span = parsed;
                return true;
            default:
                span = TimeSpan.Zero;
                return false;
        }
    }
}
