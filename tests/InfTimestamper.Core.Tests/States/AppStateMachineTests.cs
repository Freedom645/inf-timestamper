using InfTimestamper.Core.States;

namespace InfTimestamper.Core.Tests.States;

public class AppStateMachineTests
{
    [Fact]
    public void InitialState_IsInitial()
    {
        var sm = new AppStateMachine();
        Assert.Equal(AppState.Initial, sm.State);
        Assert.Null(sm.SubState);
    }

    [Fact]
    public void Start_TransitionsToWaitingForStream()
    {
        var sm = new AppStateMachine();
        sm.Start();
        Assert.Equal(AppState.WaitingForStream, sm.State);
    }

    [Fact]
    public void Start_ThrowsWhenNotInInitial()
    {
        var sm = new AppStateMachine();
        sm.Start();
        Assert.Throws<InvalidStateTransitionException>(() => sm.Start());
    }

    [Fact]
    public void Stop_FromWaitingForStream_ReturnsToInitial()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.Stop();
        Assert.Equal(AppState.Initial, sm.State);
    }

    [Fact]
    public void Stop_FromRecording_GoesToRecordingEnded()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        sm.Stop();
        Assert.Equal(AppState.RecordingEnded, sm.State);
    }

    [Fact]
    public void Stop_FromInitial_Throws()
    {
        var sm = new AppStateMachine();
        Assert.Throws<InvalidStateTransitionException>(() => sm.Stop());
    }

    [Fact]
    public void DetectStreamStart_TransitionsFromWaitingToRecording()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        Assert.Equal(AppState.Recording, sm.State);
    }

    [Fact]
    public void ForceStart_TransitionsFromWaitingToRecording()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.ForceStart();
        Assert.Equal(AppState.Recording, sm.State);
    }

    [Fact]
    public void DetectStreamEnd_TransitionsFromRecordingToRecordingEnded()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        sm.DetectStreamEnd();
        Assert.Equal(AppState.RecordingEnded, sm.State);
    }

    [Fact]
    public void OpenFile_FromInitial_GoesToRecordingEnded()
    {
        var sm = new AppStateMachine();
        sm.OpenFile();
        Assert.Equal(AppState.RecordingEnded, sm.State);
    }

    [Fact]
    public void Resume_FromRecordingEnded_GoesBackToRecording()
    {
        var sm = new AppStateMachine();
        sm.OpenFile();
        sm.Resume();
        Assert.Equal(AppState.Recording, sm.State);
    }

    [Fact]
    public void Reset_FromRecordingEnded_ReturnsToInitial()
    {
        var sm = new AppStateMachine();
        sm.OpenFile();
        sm.Reset();
        Assert.Equal(AppState.Initial, sm.State);
    }

    [Fact]
    public void StateChangedEvent_FiresOnTransition()
    {
        var sm = new AppStateMachine();
        AppState? old = null, next = null;
        sm.StateChanged += (_, e) => { old = e.OldState; next = e.NewState; };
        sm.Start();
        Assert.Equal(AppState.Initial, old);
        Assert.Equal(AppState.WaitingForStream, next);
    }

    [Fact]
    public void DetectSubState_AdvancesSubState_WhenRecording()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        sm.DetectSubState(RecordingSubState.SongSelect);
        Assert.Equal(RecordingSubState.SongSelect, sm.SubState);
    }

    [Fact]
    public void DetectSubState_ThrowsWhenNotInRecording()
    {
        var sm = new AppStateMachine();
        Assert.Throws<InvalidStateTransitionException>(() =>
            sm.DetectSubState(RecordingSubState.SongSelect));
    }

    [Fact]
    public void DetectSubState_DoesNotFireEventForSameSubState()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        sm.DetectSubState(RecordingSubState.SongSelect);

        var fireCount = 0;
        sm.SubStateChanged += (_, _) => fireCount++;
        sm.DetectSubState(RecordingSubState.SongSelect);
        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void ExitingRecording_ClearsSubState()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();
        sm.DetectSubState(RecordingSubState.PlayStart);
        sm.DetectStreamEnd();
        Assert.Null(sm.SubState);
    }

    [Fact]
    public void FullRecordingCycle_VisitsAllSubStatesInOrder()
    {
        var sm = new AppStateMachine();
        sm.Start();
        sm.DetectStreamStart();

        var subStateLog = new List<RecordingSubState?>();
        sm.SubStateChanged += (_, e) => subStateLog.Add(e.NewSubState);

        sm.DetectSubState(RecordingSubState.SongSelect);
        sm.DetectSubState(RecordingSubState.PlayStart);
        sm.DetectSubState(RecordingSubState.Result);
        sm.DetectSubState(RecordingSubState.SongSelect);

        Assert.Equal(
            new RecordingSubState?[]
            {
                RecordingSubState.SongSelect,
                RecordingSubState.PlayStart,
                RecordingSubState.Result,
                RecordingSubState.SongSelect,
            },
            subStateLog);
    }
}
