using InfTimestamper.Core.Obs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Tests.Obs;

public class ObsScreenshotCaptureTests
{
    private const string Source = "INFINITAS";
    private static readonly TimeSpan FastPeriod = TimeSpan.FromMilliseconds(30);

    [Fact]
    public async Task Capture_OnSuccess_RaisesScreenshotCapturedEvent()
    {
        var conn = new FakeObsConnection
        {
            ScreenshotHandler = _ => Task.FromResult(new ObsScreenshot(new byte[] { 1, 2, 3 }, DateTimeOffset.Now)),
        };

        var captured = new List<ObsScreenshot>();
        await using var capture = new ObsScreenshotCapture(conn, NullLogger<ObsScreenshotCapture>.Instance, FastPeriod);
        capture.ScreenshotCaptured += (_, e) => captured.Add(e.Screenshot);

        capture.Start(Source);

        await WaitUntilAsync(() => captured.Count >= 2);
        await capture.StopAsync();

        Assert.NotEmpty(captured);
        Assert.Equal(new byte[] { 1, 2, 3 }, captured[0].PngBytes);
    }

    [Fact]
    public async Task Capture_OnFailure_IncrementsConsecutiveFailureCount()
    {
        var conn = new FakeObsConnection
        {
            ScreenshotHandler = _ => Task.FromException<ObsScreenshot>(new InvalidOperationException("取得不可")),
        };
        var logger = new TestLogger<ObsScreenshotCapture>();

        await using var capture = new ObsScreenshotCapture(conn, logger, FastPeriod);
        var failures = new List<int>();
        capture.CaptureFailed += (_, e) => failures.Add(e.ConsecutiveFailureCount);

        capture.Start(Source);

        await WaitUntilAsync(() => capture.ConsecutiveFailureCount >= ObsScreenshotCapture.FailureLogThreshold);
        await capture.StopAsync();

        Assert.True(capture.ConsecutiveFailureCount >= ObsScreenshotCapture.FailureLogThreshold);
        Assert.Contains(failures, c => c == ObsScreenshotCapture.FailureLogThreshold);
        // 連続失敗で Warn ログが 1 回出ていること（同じ failure run 中に複数回出さない）
        Assert.Equal(1, logger.CountAt(LogLevel.Warning));
    }

    [Fact]
    public async Task Capture_RecoveryAfterFailureBurst_LogsInformation()
    {
        int call = 0;
        var conn = new FakeObsConnection
        {
            ScreenshotHandler = _ =>
            {
                var c = Interlocked.Increment(ref call);
                if (c <= ObsScreenshotCapture.FailureLogThreshold)
                    return Task.FromException<ObsScreenshot>(new InvalidOperationException("失敗中"));
                return Task.FromResult(new ObsScreenshot(new byte[] { 9 }, DateTimeOffset.Now));
            },
        };
        var logger = new TestLogger<ObsScreenshotCapture>();

        await using var capture = new ObsScreenshotCapture(conn, logger, FastPeriod);
        capture.Start(Source);

        await WaitUntilAsync(() => logger.CountAt(LogLevel.Information) >= 1);
        await capture.StopAsync();

        Assert.Equal(1, logger.CountAt(LogLevel.Warning));
        Assert.True(logger.CountAt(LogLevel.Information) >= 1);
        Assert.Equal(0, capture.ConsecutiveFailureCount);
    }

    [Fact]
    public async Task Start_WhenAlreadyRunning_Throws()
    {
        var conn = new FakeObsConnection
        {
            ScreenshotHandler = _ => Task.FromResult(new ObsScreenshot(Array.Empty<byte>(), DateTimeOffset.Now)),
        };
        await using var capture = new ObsScreenshotCapture(conn, NullLogger<ObsScreenshotCapture>.Instance, FastPeriod);

        capture.Start(Source);
        try
        {
            Assert.Throws<InvalidOperationException>(() => capture.Start(Source));
        }
        finally
        {
            await capture.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_StopsLoop()
    {
        var conn = new FakeObsConnection
        {
            ScreenshotHandler = _ => Task.FromResult(new ObsScreenshot(Array.Empty<byte>(), DateTimeOffset.Now)),
        };
        await using var capture = new ObsScreenshotCapture(conn, NullLogger<ObsScreenshotCapture>.Instance, FastPeriod);

        capture.Start(Source);
        await Task.Delay(100);
        await capture.StopAsync();

        Assert.False(capture.IsRunning);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(5);
        }
        throw new TimeoutException("条件が制限時間内に成立しませんでした。");
    }
}
