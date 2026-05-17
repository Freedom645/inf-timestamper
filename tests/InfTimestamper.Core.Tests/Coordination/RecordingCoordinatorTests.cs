using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Obs;
using InfTimestamper.Core.Tests.Recognition;
using InfTimestamper.Core.Threading;

namespace InfTimestamper.Core.Tests.Coordination;

public class RecordingCoordinatorTests
{
    private static readonly ObsConnectionOptions DefaultObs = new("127.0.0.1", 4455, "");

    private static RecordingCoordinator BuildCoordinator(
        out AppStateMachine state,
        out RecognitionPipeline pipeline,
        out FakeObsConnection sharedConnection,
        string source = "INF")
    {
        state = new AppStateMachine();
        var recognizer = new FrameRecognizer(
            new ImageHasher(),
            new NoOpOcrService(),
            HashResource.Empty(),
            RoiResource.Empty());
        pipeline = new RecognitionPipeline(recognizer);
        var conn = new FakeObsConnection();
        sharedConnection = conn;
        var dispatcher = ImmediateUiDispatcher.Instance;

        var coordinator = new RecordingCoordinator(
            state,
            pipeline,
            dispatcher,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)),
            captureFactory: c => new ObsScreenshotCapture(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<ObsScreenshotCapture>.Instance, TimeSpan.FromMilliseconds(50)));

        coordinator.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = DefaultObs,
            GameSourceName = source,
        });
        return coordinator;
    }

    [Fact]
    public async Task Start_TransitionsToWaitingForStream_LeavesObsManagerRunning()
    {
        var coordinator = BuildCoordinator(out var state, out _, out _);
        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState != ObsConnectionManagerState.Idle, 1000);

            Assert.NotEqual(ObsConnectionManagerState.Idle, coordinator.CurrentObsState);
        }
    }

    [Fact]
    public async Task ForceStart_AfterConnection_StartsScreenshotCapture()
    {
        var coordinator = BuildCoordinator(out var state, out _, out var conn);
        conn.ScreenshotHandler = _ => Task.FromResult(new ObsScreenshot(new byte[] { 1 }, DateTimeOffset.Now));

        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);

            state.ForceStart();
            // 画面取得が始まると ScreenshotHandler が呼ばれる
            await WaitUntilAsync(() => conn.ConnectAttempts >= 1, 1000);
            // 数フレーム待つ
            await Task.Delay(150);
        }
    }

    [Fact]
    public async Task Stop_TransitionsToRecordingEnded_DisconnectsObs()
    {
        var coordinator = BuildCoordinator(out var state, out _, out var conn);
        await using (coordinator)
        {
            state.Start();
            await WaitUntilAsync(() => coordinator.CurrentObsState == ObsConnectionManagerState.Connected, 1000);

            state.ForceStart();
            state.Stop();

            await WaitUntilAsync(() => conn.DisposeCount > 0, 1000);
        }
    }

    [Fact]
    public void OnPlayStartedFromPipeline_BubblesViaCoordinator()
    {
        var coordinator = BuildCoordinator(out _, out var pipeline, out _);
        PlayStartedEventArgs? captured = null;
        coordinator.PlayStarted += (_, e) => captured = e;

        pipeline.InjectRecognition(new FrameRecognition(
            DateTimeOffset.Now,
            RecognizedState.PlayStart,
            null,
            new Dictionary<string, string> { ["title"] = "Sample" }));

        Assert.NotNull(captured);
        Assert.Equal("Sample", captured!.Fields["title"]);
    }

    [Fact]
    public void OnPlayResultDetectedFromPipeline_BubblesViaCoordinator()
    {
        var coordinator = BuildCoordinator(out _, out var pipeline, out _);
        PlayResultEventArgs? captured = null;
        coordinator.PlayResultDetected += (_, e) => captured = e;

        pipeline.InjectRecognition(new FrameRecognition(
            DateTimeOffset.Now,
            RecognizedState.PlayStart, null, new Dictionary<string, string>()));
        pipeline.InjectRecognition(new FrameRecognition(
            DateTimeOffset.Now.AddSeconds(60),
            RecognizedState.Result, null,
            new Dictionary<string, string> { ["miss_count"] = "3" }));

        Assert.NotNull(captured);
        Assert.Equal("3", captured!.Fields["miss_count"]);
    }

    [Fact]
    public void Configure_WithoutStreamObs_DoesNotStartConnection()
    {
        var state = new AppStateMachine();
        var recognizer = new FrameRecognizer(new ImageHasher(), new NoOpOcrService(), HashResource.Empty(), RoiResource.Empty());
        var pipeline = new RecognitionPipeline(recognizer);
        var conn = new FakeObsConnection();

        var coordinator = new RecordingCoordinator(
            state,
            pipeline,
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)),
            captureFactory: c => new ObsScreenshotCapture(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<ObsScreenshotCapture>.Instance, TimeSpan.FromMilliseconds(50)));

        // StreamObs を設定しないまま Start
        state.Start();
        Assert.Equal(ObsConnectionManagerState.Idle, coordinator.CurrentObsState);
        Assert.Equal(0, conn.ConnectAttempts);
    }

    private static async Task WaitUntilAsync(Func<bool> cond, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (cond()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("条件が成立しませんでした。");
    }
}
