using InfTimestamper.Core.Recognition;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Recognition;

public class DebugFrameStoreTests
{
    private const int SmallPngLen = 16;

    [Fact]
    public void Save_WhenDisabled_DoesNothing()
    {
        using var temp = new TempDirectory();
        var store = new DebugFrameStore(temp.Path, enabled: false);

        var path = store.Save(MakeDummyPng(), DateTimeOffset.Now);

        Assert.Null(path);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, DebugFrameStore.SubDirectoryName)));
    }

    [Fact]
    public void Save_WhenEnabled_WritesFileWithExpectedNameFormat()
    {
        using var temp = new TempDirectory();
        var store = new DebugFrameStore(temp.Path, enabled: true);

        var capturedAt = new DateTimeOffset(2026, 5, 17, 18, 30, 45, 123, TimeSpan.FromHours(9));
        var path = store.Save(MakeDummyPng(), capturedAt, "low_confidence");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        var fileName = Path.GetFileName(path)!;
        Assert.StartsWith("frame_20260517_183045_123", fileName);
        Assert.EndsWith(".png", fileName);
        Assert.Contains("low_confidence", fileName);

        var dir = Path.GetDirectoryName(path)!;
        Assert.EndsWith("20260517", dir);
    }

    [Fact]
    public void Save_TrimsOldestWhenLimitExceeded()
    {
        using var temp = new TempDirectory();
        const int max = 3;
        var store = new DebugFrameStore(temp.Path, enabled: true, maxFiles: max);

        var basetime = new DateTimeOffset(2026, 5, 17, 18, 0, 0, TimeSpan.FromHours(9));
        for (int i = 0; i < max + 2; i++)
        {
            var p = store.Save(MakeDummyPng(), basetime.AddSeconds(i));
            Assert.NotNull(p);
        }

        var allFiles = Directory.GetFiles(Path.Combine(temp.Path, DebugFrameStore.SubDirectoryName), "*.png", SearchOption.AllDirectories);
        Assert.True(allFiles.Length <= max, $"上限 {max} を超えている: {allFiles.Length}");

        // 残っているのは新しい時刻のものであること（最古から削除されている）
        var names = allFiles.Select(Path.GetFileName).ToList();
        Assert.DoesNotContain("frame_20260517_180000_000.png", names);
        Assert.DoesNotContain("frame_20260517_180001_000.png", names);
        Assert.Contains("frame_20260517_180004_000.png", names);
    }

    [Fact]
    public void Save_NullOrEmpty_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var store = new DebugFrameStore(temp.Path, enabled: true);

        Assert.Null(store.Save(null!, DateTimeOffset.Now));
        Assert.Null(store.Save(Array.Empty<byte>(), DateTimeOffset.Now));
    }

    private static byte[] MakeDummyPng()
    {
        // 実際の PNG ヘッダは不要。バイト列がファイルとして書ければよいテスト
        var bytes = new byte[SmallPngLen];
        new Random(0).NextBytes(bytes);
        return bytes;
    }
}
