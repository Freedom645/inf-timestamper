using System.Collections.ObjectModel;
using System.Text;
using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.States;
using InfTimestamper.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.ViewModels;

public sealed class MainWindowViewModel : ObservableBase
{
    public const string DefaultFormat = "$timestamp $title [$diff_s $level]";

    private readonly AppStateMachine _stateMachine;
    private readonly IClipboardService _clipboard;
    private readonly ILogger<MainWindowViewModel> _logger;

    private StreamRecord _record = new();
    private string _format = DefaultFormat;
    private string _hintText = string.Empty;

    public MainWindowViewModel(
        AppStateMachine stateMachine,
        IClipboardService clipboard,
        ILogger<MainWindowViewModel>? logger = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;

        _stateMachine.StateChanged += OnStateMachineChanged;

        StartCommand = new RelayCommand(ExecuteStart, () => State == AppState.Initial);
        ForceStartCommand = new RelayCommand(ExecuteForceStart, () => State == AppState.WaitingForStream);
        StopCommand = new RelayCommand(ExecuteStop,
            () => State is AppState.WaitingForStream or AppState.Recording);
        ResumeCommand = new RelayCommand(ExecuteResume, () => State == AppState.RecordingEnded);
        ResetCommand = new RelayCommand(ExecuteReset, () => State == AppState.RecordingEnded);
        CopyCommand = new RelayCommand(ExecuteCopy, () => Timestamps.Count > 0);
    }

    public ObservableCollection<TimestampViewModel> Timestamps { get; } = new();

    public AppState State => _stateMachine.State;

    public string GameName => "INFINITAS";

    public string StateLabel => State switch
    {
        AppState.Initial => "初期状態",
        AppState.WaitingForStream => "配信開始待ち",
        AppState.Recording => "記録中",
        AppState.RecordingEnded => "記録終了",
        _ => "-",
    };

    public string PrimaryButtonText => State switch
    {
        AppState.Initial => "開始",
        AppState.WaitingForStream => "強制開始",
        AppState.Recording => "記録停止",
        AppState.RecordingEnded => "記録再開",
        _ => "-",
    };

    public RelayCommand PrimaryCommand => State switch
    {
        AppState.Initial => StartCommand,
        AppState.WaitingForStream => ForceStartCommand,
        AppState.Recording => StopCommand,
        AppState.RecordingEnded => ResumeCommand,
        _ => StartCommand,
    };

    public string HintText
    {
        get => _hintText;
        set => SetField(ref _hintText, value ?? string.Empty);
    }

    public string Format
    {
        get => _format;
        set
        {
            var next = value ?? string.Empty;
            if (!SetField(ref _format, next)) return;
            foreach (var ts in Timestamps)
                ts.UpdateFormat(next);
        }
    }

    public DateTimeOffset? StreamStartedAt
        => _record.Stream.StartedAt == default ? null : _record.Stream.StartedAt;

    public string StreamStartedAtText => StreamStartedAt is null
        ? "-"
        : StreamStartedAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    public int TimestampCount => Timestamps.Count;

    public bool CanReset => State == AppState.RecordingEnded;

    public RelayCommand StartCommand { get; }
    public RelayCommand ForceStartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand CopyCommand { get; }

    public void AddTimestamp(TimestampEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        var vm = new TimestampViewModel(entry, _record.Stream.StartedAt, _format);
        InsertSorted(vm);
        _record.Timestamps.Add(entry);
        _record.UpdatedAt = DateTimeOffset.Now;
        RaisePropertyChanged(nameof(TimestampCount));
        CopyCommand.RaiseCanExecuteChanged();
    }

    public void SetStreamStartedAt(DateTimeOffset startedAt)
    {
        _record.Stream.StartedAt = startedAt;
        RaisePropertyChanged(nameof(StreamStartedAt));
        RaisePropertyChanged(nameof(StreamStartedAtText));
        foreach (var ts in Timestamps)
            ts.UpdateStreamStartedAt(startedAt);
    }

    internal StreamRecord Record => _record;

    private void ExecuteStart()
    {
        TryStateOp(() => _stateMachine.Start(), "開始");
    }

    private void ExecuteForceStart()
    {
        TryStateOp(() =>
        {
            _stateMachine.ForceStart();
            SetStreamStartedAt(DateTimeOffset.Now);
        }, "強制開始");
    }

    private void ExecuteStop()
    {
        TryStateOp(() =>
        {
            _stateMachine.Stop();
            if (State == AppState.RecordingEnded)
                _record.Stream.EndedAt = DateTimeOffset.Now;
        }, "停止");
    }

    private void ExecuteResume()
    {
        TryStateOp(() =>
        {
            _stateMachine.Resume();
            _record.Stream.EndedAt = null;
        }, "再開");
    }

    private void ExecuteReset()
    {
        TryStateOp(() =>
        {
            _stateMachine.Reset();
            _record = new StreamRecord();
            Timestamps.Clear();
            RaisePropertyChanged(nameof(StreamStartedAt));
            RaisePropertyChanged(nameof(StreamStartedAtText));
            RaisePropertyChanged(nameof(TimestampCount));
            CopyCommand.RaiseCanExecuteChanged();
        }, "リセット");
    }

    private void ExecuteCopy()
    {
        if (Timestamps.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var ts in Timestamps)
            sb.AppendLine(ts.DisplayText);
        try
        {
            _clipboard.SetText(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "クリップボードコピーに失敗しました。");
        }
    }

    private void OnStateMachineChanged(object? sender, StateChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(State));
        RaisePropertyChanged(nameof(StateLabel));
        RaisePropertyChanged(nameof(PrimaryButtonText));
        RaisePropertyChanged(nameof(PrimaryCommand));
        RaisePropertyChanged(nameof(CanReset));

        StartCommand.RaiseCanExecuteChanged();
        ForceStartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
    }

    private void TryStateOp(Action op, string opName)
    {
        try { op(); }
        catch (InvalidStateTransitionException ex)
        {
            _logger.LogWarning(ex, "{Op} 操作が現在の状態 {State} では実行できません。", opName, State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Op} 操作中に予期しないエラーが発生しました。", opName);
        }
    }

    private void InsertSorted(TimestampViewModel vm)
    {
        // 常に PlayStartedAt の昇順を維持
        var index = 0;
        for (; index < Timestamps.Count; index++)
        {
            if (Timestamps[index].PlayStartedAt > vm.PlayStartedAt) break;
        }
        Timestamps.Insert(index, vm);
    }
}
