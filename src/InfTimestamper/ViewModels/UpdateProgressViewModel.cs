namespace InfTimestamper.ViewModels;

public sealed class UpdateProgressViewModel : ObservableBase
{
    private int _progressPercent;
    private string _statusText = "アップデートを確認しています...";
    private bool _isCompleted;
    private bool _hasFailed;

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value ?? string.Empty);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    public bool HasFailed
    {
        get => _hasFailed;
        set => SetField(ref _hasFailed, value);
    }
}
