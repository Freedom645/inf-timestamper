using System.Text;
using InfTimestamper.Core.Sdvx;
using InfTimestamper.Core.Tests.TestHelpers;

namespace InfTimestamper.Core.Tests.Sdvx;

/// <summary>
/// FileSystemWatcher のタイミングに依存せず、ログファイルへ追記して <c>Poll()</c> を直接呼ぶ。
/// 行の書式は SDVX Helper の <c>src/logger.py</c>（<c>[asctime] [level] file:line:func - message</c>）に合わせている。
/// </summary>
public class SdvxHelperLogTailTests
{
    private const string ModeChangeToPlay =
        "[2026-09-22 20:15:30,123] [INFO] sdvx_helper.pyw:781:_on_mode_changed - モード変更: select → play";

    private static string Line(string from, string to, string time = "2026-09-22 20:15:30,123")
        => $"[{time}] [INFO] sdvx_helper.pyw:781:_on_mode_changed - モード変更: {from} → {to}";

    private sealed class Harness : IDisposable
    {
        private readonly TempDirectory _dir = new();

        public Harness(SdvxHelperLogTail tail, bool createLog = true, long offset = 0)
        {
            Directory.CreateDirectory(Path.Combine(_dir.Path, SdvxHelperLogTail.LogDirectoryName));
            LogPath = SdvxHelperLogTail.ResolveLogPath(_dir.Path);
            if (createLog) File.WriteAllText(LogPath, string.Empty);
            tail.ConfigureForTest(LogPath, offset);
        }

        public string LogPath { get; }

        public void Append(string text)
            => File.AppendAllText(LogPath, text, new UTF8Encoding(false));

        public void Dispose() => _dir.Dispose();
    }

    [Fact]
    public void TransitionToPlay_RaisesPlayEnteredWithTheLoggedTime()
    {
        using var tail = new SdvxHelperLogTail();
        var entered = new List<DateTimeOffset>();
        tail.PlayEntered += (_, at) => entered.Add(at);
        using var harness = new Harness(tail);

        harness.Append(ModeChangeToPlay + "\n");
        tail.Poll();

        var expected = new DateTimeOffset(new DateTime(2026, 9, 22, 20, 15, 30, 123, DateTimeKind.Local));
        Assert.Equal(new[] { expected }, entered);
    }

    [Theory]
    [InlineData("init", "select")]
    [InlineData("select", "detect")]
    [InlineData("play", "result")]
    [InlineData("result", "select")]
    public void OtherTransitions_DoNotRaise(string from, string to)
    {
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail);

        harness.Append(Line(from, to) + "\n");
        tail.Poll();

        Assert.Equal(0, count);
    }

    [Fact]
    public void RetryFromResult_IsAlsoAPlayStart()
    {
        // リザルト画面からのリトライは選曲も曲決定も通らない。ログにはこの遷移だけが出る
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail);

        harness.Append(Line("result", "play") + "\n");
        tail.Poll();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Poll_OnlyReadsAppendedText()
    {
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail);

        harness.Append(ModeChangeToPlay + "\n");
        tail.Poll();
        tail.Poll();   // 追記が無ければ何も起きない
        harness.Append(Line("play", "result") + "\n" + Line("result", "play", "2026-09-22 20:20:00,000") + "\n");
        tail.Poll();

        Assert.Equal(2, count);
    }

    [Fact]
    public void LinesBeforeTheStartOffset_AreIgnored()
    {
        // 監視開始前に書かれていた行（起動前のプレイ）は再生しない
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail, createLog: false);
        harness.Append(ModeChangeToPlay + "\n");
        tail.ConfigureForTest(harness.LogPath, new FileInfo(harness.LogPath).Length);

        tail.Poll();
        Assert.Equal(0, count);

        harness.Append(Line("result", "play") + "\n");
        tail.Poll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void PartialLine_IsHeldUntilTheNewlineArrives()
    {
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail);

        harness.Append(ModeChangeToPlay[..40]);
        tail.Poll();
        Assert.Equal(0, count);

        harness.Append(ModeChangeToPlay[40..] + "\n");
        tail.Poll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Rotation_ShrinkingFile_IsReadFromTheStart()
    {
        using var tail = new SdvxHelperLogTail();
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        using var harness = new Harness(tail);

        harness.Append(Line("play", "result") + "\n" + Line("result", "select") + "\n" + Line("select", "detect") + "\n");
        tail.Poll();

        // RotatingFileHandler が新しい（短い）ファイルに置き換えた
        File.WriteAllText(harness.LogPath, ModeChangeToPlay + "\n", new UTF8Encoding(false));
        tail.Poll();

        Assert.Equal(1, count);
    }

    [Fact]
    public void MissingLogFile_IsNotAnError()
    {
        using var tail = new SdvxHelperLogTail();
        using var harness = new Harness(tail, createLog: false);

        tail.Poll();   // 例外を投げない

        harness.Append(ModeChangeToPlay + "\n");
        var count = 0;
        tail.PlayEntered += (_, _) => count++;
        tail.Poll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void LineWithoutTimestamp_UsesNow()
    {
        using var tail = new SdvxHelperLogTail();
        DateTimeOffset? at = null;
        tail.PlayEntered += (_, t) => at = t;
        using var harness = new Harness(tail);

        var before = DateTimeOffset.Now;
        harness.Append("モード変更: select → play\n");
        tail.Poll();

        Assert.NotNull(at);
        Assert.InRange(at!.Value, before.AddSeconds(-1), DateTimeOffset.Now.AddSeconds(1));
    }

    [Fact]
    public void Start_ThrowsWhenTheHelperDirectoryDoesNotExist()
    {
        using var tail = new SdvxHelperLogTail();
        Assert.Throws<DirectoryNotFoundException>(
            () => tail.Start(Path.Combine(Path.GetTempPath(), "sdvx-missing-" + Guid.NewGuid().ToString("N"))));
        Assert.False(tail.IsRunning);
    }

    [Fact]
    public void Start_CreatesTheLogDirectoryAndStartsEvenWithoutTheLogFile()
    {
        using var dir = new TempDirectory();
        using var tail = new SdvxHelperLogTail();

        tail.Start(dir.Path);

        Assert.True(tail.IsRunning);
        Assert.True(Directory.Exists(Path.Combine(dir.Path, SdvxHelperLogTail.LogDirectoryName)));
        tail.Stop();
        Assert.False(tail.IsRunning);
    }

    [Theory]
    [InlineData("[2026-09-22 20:15:30,123] x", 123)]
    [InlineData("[2026-09-22 20:15:30] x", 0)]
    public void TryParseLogTime_ReadsTheLocalTimestamp(string line, int ms)
    {
        Assert.True(SdvxHelperLogTail.TryParseLogTime(line, out var value));
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 9, 22, 20, 15, 30, ms, DateTimeKind.Local)), value);
    }

    [Fact]
    public void TryParseLogTime_ReturnsFalseForOtherLines()
    {
        Assert.False(SdvxHelperLogTail.TryParseLogTime("no timestamp here", out _));
    }
}
