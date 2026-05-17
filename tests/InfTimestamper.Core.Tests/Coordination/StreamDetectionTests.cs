using InfTimestamper.Core.Coordination;
using InfTimestamper.Core.Obs;
using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.States;
using InfTimestamper.Core.Tests.Obs;
using InfTimestamper.Core.Tests.Recognition;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Coordination;

public class StreamDetectionTests
{
    private static (RecordingCoordinator coord, AppStateMachine state, FakeObsConnection conn) Build()
    {
        var state = new AppStateMachine();
        var recognizer = new FrameRecognizer(new ImageHasher(), new NoOpOcrService(), HashResource.Empty(), RoiResource.Empty());
        var pipeline = new RecognitionPipeline(recognizer);
        var conn = new FakeObsConnection();
        var coord = new RecordingCoordinator(
            state,
            pipeline,
            ImmediateUiDispatcher.Instance,
            streamConnectionFactory: () => conn,
            managerFactory: c => new ObsConnectionManager(c, NullLogger<ObsConnectionManager>.Instance, new TestDelayProvider(), TimeSpan.FromMilliseconds(50)),
            captureFactory: c => new ObsScreenshotCapture(c, NullLogger<ObsScreenshotCapture>.Instance, TimeSpan.FromMilliseconds(50)));
        coord.Configure(new RecordingCoordinatorOptions
        {
            StreamObs = new ObsConnectionOptions("127.0.0.1", 4455, ""),
            GameSourceName = "INF",
        });
        return (coord, state, conn);
    }

    [Fact]
    public async Task ObsStreamStarted_WhileWaitingForStream_TransitionsToRecording()
    {
        var (coord, state, conn) = Build();
        await using (coord)
        {
            state.Start();
            await WaitUntilAsync(() => conn.IsConnected, 1000);

            conn.RaiseStreamStateChanged(ObsStreamState.Started, true);

            Assert.Equal(AppState.Recording, state.State);
        }
    }

    [Fact]
    public async Task ObsStreamStopped_WhileRecording_TransitionsToRecordingEnded()
    {
        var (coord, state, conn) = Build();
        await using (coord)
        {
            state.Start();
            await WaitUntilAsync(() => conn.IsConnected, 1000);
            state.ForceStart();

            conn.RaiseStreamStateChanged(ObsStreamState.Stopped, false);

            Assert.Equal(AppState.RecordingEnded, state.State);
        }
    }

    [Fact]
    public async Task ObsStreamStarted_OutsideWaitingForStream_DoesNothing()
    {
        var (coord, state, conn) = Build();
        await using (coord)
        {
            state.Start();
            await WaitUntilAsync(() => conn.IsConnected, 1000);
            state.ForceStart();
            Assert.Equal(AppState.Recording, state.State);

            // Recording 状態で Started を受けても遷移しない（既に Recording）
            conn.RaiseStreamStateChanged(ObsStreamState.Started, true);
            Assert.Equal(AppState.Recording, state.State);
        }
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
