namespace InfTimestamper.Core.States;

public sealed class AppStateMachine
{
    private AppState _state = AppState.Initial;
    private RecordingSubState? _subState;

    public AppState State => _state;
    public RecordingSubState? SubState => _subState;

    public event EventHandler<StateChangedEventArgs>? StateChanged;
    public event EventHandler<SubStateChangedEventArgs>? SubStateChanged;

    // --- メイン遷移 ---

    public void Start()
    {
        EnsureState(AppState.Initial, nameof(Start));
        Transition(AppState.WaitingForStream);
    }

    public void Stop()
    {
        switch (_state)
        {
            case AppState.WaitingForStream:
                Transition(AppState.Initial);
                break;
            case AppState.Recording:
                Transition(AppState.RecordingEnded);
                break;
            default:
                throw new InvalidStateTransitionException(_state, nameof(Stop));
        }
    }

    public void DetectStreamStart()
    {
        EnsureState(AppState.WaitingForStream, nameof(DetectStreamStart));
        Transition(AppState.Recording);
    }

    public void ForceStart()
    {
        EnsureState(AppState.WaitingForStream, nameof(ForceStart));
        Transition(AppState.Recording);
    }

    public void DetectStreamEnd()
    {
        EnsureState(AppState.Recording, nameof(DetectStreamEnd));
        Transition(AppState.RecordingEnded);
    }

    public void Resume()
    {
        EnsureState(AppState.RecordingEnded, nameof(Resume));
        Transition(AppState.Recording);
    }

    public void Reset()
    {
        EnsureState(AppState.RecordingEnded, nameof(Reset));
        Transition(AppState.Initial);
    }

    public void OpenFile()
    {
        EnsureState(AppState.Initial, nameof(OpenFile));
        Transition(AppState.RecordingEnded);
    }

    // --- サブ状態遷移（Recording 中のみ有効） ---

    public void DetectSubState(RecordingSubState newSubState)
    {
        if (_state != AppState.Recording)
            throw new InvalidStateTransitionException(_state, nameof(DetectSubState));

        if (_subState == newSubState)
            return; // 同一サブ状態の維持はノーオペ（要件: エッジ検知時のみ副作用）

        var old = _subState;
        _subState = newSubState;
        SubStateChanged?.Invoke(this, new SubStateChangedEventArgs(old, newSubState));
    }

    // --- 内部 ---

    private void EnsureState(AppState expected, string operation)
    {
        if (_state != expected)
            throw new InvalidStateTransitionException(_state, operation);
    }

    private void Transition(AppState next)
    {
        var old = _state;
        _state = next;

        // Recording を抜けた場合はサブ状態をクリア
        if (next != AppState.Recording && _subState is not null)
        {
            var oldSub = _subState;
            _subState = null;
            SubStateChanged?.Invoke(this, new SubStateChangedEventArgs(oldSub, null));
        }

        StateChanged?.Invoke(this, new StateChangedEventArgs(old, next));
    }
}
