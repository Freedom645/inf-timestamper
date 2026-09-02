using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Persistence;
using InfTimestamper.Core.Popn;
using InfTimestamper.Core.Reflux;
using InfTimestamper.Core.Settings;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Obs;
using InfTimestamper.Core.Tests.TestHelpers;
using InfTimestamper.Core.Threading;
using InfTimestamper.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.ViewModels;

/// <summary>
/// メインウィンドウのゲーム選択と、選択に応じたフォーマット / 監視ディレクトリの切り替え。
/// </summary>
public class MainWindowGameSelectionTests
{
    private sealed record Fixture(
        MainWindowViewModel Vm,
        AppStateMachine State,
        RefluxPlayWatcher Reflux,
        PopnPlayWatcher Popn,
        RecordingCoordinator Coordinator,
        string SettingsPath);

    private static Fixture Build(TempDirectory dir, AppSettings? settings = null)
    {
        var state = new AppStateMachine();
        var reflux = new RefluxPlayWatcher(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);
        var popn = new PopnPlayWatcher(NullLogger<PopnPlayWatcher>.Instance);
        var conn = new FakeObsConnection();

        var coordinator = new RecordingCoordinator(
            state,
            new Dictionary<GameId, IPlayWatcher>
            {
                [GameId.Infinitas] = reflux,
                [GameId.Popn] = popn,
            },
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(
                c, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)));

        var settingsPath = Path.Combine(dir.Path, "settings.json");
        var vm = new MainWindowViewModel(
            state,
            new FakeClipboardService(),
            new FakeDialogService(),
            new JsonRecordStore(),
            settings ?? TestSettingsFactory.CreateDefault(),
            new SettingsStore(),
            settingsPath);
        vm.BindCoordinator(coordinator);

        return new Fixture(vm, state, reflux, popn, coordinator, settingsPath);
    }

    private static AppSettings SettingsWithBothGames()
    {
        var settings = TestSettingsFactory.CreateDefault();
        settings.Infinitas.TimestampFormat = "$timestamp $title [$diff_s $level]";
        settings.Infinitas.RefluxDirectory = @"C:\reflux";
        settings.Popn.TimestampFormat = "$timestamp $title ($rank, $medal)";
        settings.Popn.TrackerDirectory = @"C:\popn";
        return settings;
    }

    [Fact]
    public void AvailableGames_ListsEveryGameWithDisplayName()
    {
        using var dir = new TempDirectory();
        var f = Build(dir);

        Assert.Equal(GameCatalog.AllGames.Count, f.Vm.AvailableGames.Count);
        foreach (var choice in f.Vm.AvailableGames)
            Assert.Equal(GameCatalog.DisplayName(choice.Id), choice.DisplayName);
    }

    [Fact]
    public void DefaultSelection_IsInfinitas()
    {
        using var dir = new TempDirectory();
        var f = Build(dir);

        Assert.Equal(GameId.Infinitas, f.Vm.SelectedGame);
        Assert.True(f.Vm.IsGameSelectable);
    }

    [Fact]
    public void SelectedGame_IsRestoredFromSettings()
    {
        using var dir = new TempDirectory();
        var settings = SettingsWithBothGames();
        settings.General.SelectedGame = GameIdExtensions.PopnSerialized;

        var f = Build(dir, settings);

        Assert.Equal(GameId.Popn, f.Vm.SelectedGame);
        Assert.Equal("$timestamp $title ($rank, $medal)", f.Vm.Format);
    }

    [Fact]
    public void ChangingGame_SwitchesFormatAndWatchTarget()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        Assert.Equal("$timestamp $title [$diff_s $level]", f.Vm.Format);
        Assert.Equal(@"C:\reflux", f.Coordinator.Options.WatchTarget);

        f.Vm.SelectedGame = GameId.Popn;

        Assert.Equal("$timestamp $title ($rank, $medal)", f.Vm.Format);
        Assert.Equal(GameId.Popn, f.Coordinator.Options.Game);
        Assert.Equal(@"C:\popn", f.Coordinator.Options.WatchTarget);
    }

    [Fact]
    public void ChangingGame_PersistsSelectionToSettings()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        f.Vm.SelectedGame = GameId.Popn;

        var reloaded = new SettingsStore().Load(f.SettingsPath);
        Assert.Equal(GameId.Popn, reloaded.ResolveSelectedGame());
    }

    [Fact]
    public void ChangingGame_UpdatesRecordGame()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        f.Vm.SelectedGame = GameId.Popn;

        Assert.Equal(GameId.Popn, f.Vm.Record.Game);
    }

    [Fact]
    public void GameSelection_IsLockedOnceRecordingStarts()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        f.Vm.StartCommand.Execute(null);
        Assert.False(f.Vm.IsGameSelectable);

        f.Vm.SelectedGame = GameId.Popn;

        Assert.Equal(GameId.Infinitas, f.Vm.SelectedGame);
        Assert.Equal(GameId.Infinitas, f.Coordinator.Options.Game);
    }

    [Fact]
    public void Reset_KeepsSelectedGame()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        f.Vm.SelectedGame = GameId.Popn;
        f.Vm.StartCommand.Execute(null);
        f.Vm.StopCommand.Execute(null);
        f.Vm.ResetCommand.Execute(null);

        Assert.Equal(GameId.Popn, f.Vm.SelectedGame);
        Assert.Equal(GameId.Popn, f.Vm.Record.Game);
    }

    [Fact]
    public void PopnWatcher_PlayStartedThenResult_BuildsTimestampEntry()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());
        f.Vm.SelectedGame = GameId.Popn;

        using var harness = new PopnTestHarness(f.Popn);
        harness.EnterPlay();

        Assert.Equal(1, f.Vm.TimestampCount);

        harness.LeavePlay();
        harness.WriteResult(new PopnResultJson
        {
            Time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Score = 93578,
            RankName = "AA",
            MedalName = "銅星",
            Judge = new PopnJudgeJson { Bad = 2 },
            Music = new PopnMusicJson { Title = "Sorrows", Sheet = "NORMAL", Level = 29 },
        });

        var entry = f.Vm.Timestamps[0].Entry;
        Assert.True(entry.TryGetFieldAsString("title", out var title));
        Assert.Equal("Sorrows", title);
        Assert.True(entry.TryGetFieldAsString("rank", out var rank));
        Assert.Equal("AA", rank);
        Assert.True(entry.TryGetFieldAsString("medal", out var medal));
        Assert.Equal("銅星", medal);

        // pop'n フォーマットで展開される
        Assert.Contains("Sorrows (AA, 銅星)", f.Vm.Timestamps[0].DisplayText);
    }

    [Fact]
    public void LoadingRecord_AdoptsTheFilesGame()
    {
        using var dir = new TempDirectory();
        var f = Build(dir, SettingsWithBothGames());

        var path = Path.Combine(dir.Path, "popn.json");
        var record = new StreamRecord
        {
            Game = GameId.Popn,
            Stream = new StreamInfo
            {
                StartedAt = DateTimeOffset.Now.AddHours(-1),
                EndedAt = DateTimeOffset.Now,
            },
        };
        new JsonRecordStore().SaveAtomic(record, path);

        var dialog = new FakeDialogService { OpenFileResult = path };
        var f2 = BuildWithDialog(dir, SettingsWithBothGames(), dialog);
        f2.Vm.OpenRecordCommand.Execute(null);

        Assert.Equal(GameId.Popn, f2.Vm.SelectedGame);
        Assert.Equal("$timestamp $title ($rank, $medal)", f2.Vm.Format);
    }

    private static Fixture BuildWithDialog(TempDirectory dir, AppSettings settings, FakeDialogService dialog)
    {
        var state = new AppStateMachine();
        var reflux = new RefluxPlayWatcher(NullLogger<RefluxPlayWatcher>.Instance, TimeSpan.Zero);
        var popn = new PopnPlayWatcher(NullLogger<PopnPlayWatcher>.Instance);
        var conn = new FakeObsConnection();

        var coordinator = new RecordingCoordinator(
            state,
            new Dictionary<GameId, IPlayWatcher>
            {
                [GameId.Infinitas] = reflux,
                [GameId.Popn] = popn,
            },
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(
                c, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)));

        var settingsPath = Path.Combine(dir.Path, "settings2.json");
        var vm = new MainWindowViewModel(
            state,
            new FakeClipboardService(),
            dialog,
            new JsonRecordStore(),
            settings,
            new SettingsStore(),
            settingsPath);
        vm.BindCoordinator(coordinator);

        return new Fixture(vm, state, reflux, popn, coordinator, settingsPath);
    }
}
