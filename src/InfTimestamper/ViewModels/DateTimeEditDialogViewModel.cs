using System.Globalization;

namespace InfTimestamper.ViewModels;

public sealed class DateTimeEditDialogViewModel : ObservableBase
{
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly IReadOnlyList<DateTimeOffset> _initialValues;
    private readonly DateTimeOffset _initialMin;
    private readonly TimeSpan _offset;

    private string _editText;
    private bool _isTextValid = true;

    public DateTimeEditDialogViewModel(IReadOnlyList<DateTimeOffset> currentValues)
    {
        if (currentValues is null || currentValues.Count == 0)
            throw new ArgumentException("編集対象が指定されていません。", nameof(currentValues));

        _initialValues = currentValues;
        _initialMin = currentValues.Min();
        _offset = _initialMin.Offset;
        _editText = _initialMin.ToOffset(_offset).LocalDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

        ShiftCommand = new ShiftCommand(ApplyShift);
        ConfirmCommand = new RelayCommand(Confirm, () => IsTextValid);
        CancelCommand = new RelayCommand(Cancel);
    }

    public bool IsMultiple => _initialValues.Count > 1;
    public bool IsSingle => !IsMultiple;
    public int EditingCount => _initialValues.Count;

    public string EditText
    {
        get => _editText;
        set
        {
            var next = value ?? string.Empty;
            if (!SetField(ref _editText, next)) return;
            IsTextValid = TryParseEditText(out _);
            ConfirmCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsTextValid
    {
        get => _isTextValid;
        private set => SetField(ref _isTextValid, value);
    }

    public IReadOnlyList<DateTimeOffset>? Result { get; private set; }
    public bool? DialogResult { get; private set; }

    public event Action? RequestClose;

    public ShiftCommand ShiftCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void ApplyShift(TimeSpan delta)
    {
        // 単一/複数いずれも EditText を相対シフトする（複数編集の表示は最小値の射影）
        if (!TryParseEditText(out var current))
            current = _initialMin.LocalDateTime;

        var shifted = current.Add(delta);
        EditText = shifted.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
    }

    private bool TryParseEditText(out DateTime value)
    {
        return DateTime.TryParseExact(
            _editText,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out value);
    }

    private void Confirm()
    {
        if (!TryParseEditText(out var newMinLocal)) return;
        var newMin = new DateTimeOffset(newMinLocal, _offset);

        if (IsMultiple)
        {
            var shift = newMin - _initialMin;
            Result = _initialValues.Select(v => v + shift).ToArray();
        }
        else
        {
            Result = new[] { newMin };
        }

        DialogResult = true;
        RequestClose?.Invoke();
    }

    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}
