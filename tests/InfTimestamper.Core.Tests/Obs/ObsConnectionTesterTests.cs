using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

public class ObsConnectionTesterTests
{
    private static readonly ObsConnectionOptions Options = new("127.0.0.1", 4455, "pw");

    [Fact]
    public async Task TestAsync_OnSuccess_ReturnsServerInfo()
    {
        var conn = new FakeObsConnection
        {
            ServerInfoHandler = () => Task.FromResult(new ObsServerInfo("31.0.0", "Game")),
        };
        var tester = new ObsConnectionTester(() => conn);

        var result = await tester.TestAsync(Options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("31.0.0", result.ObsVersion);
        Assert.Equal("Game", result.CurrentScene);
        Assert.Equal(1, conn.DisposeCount);
    }

    [Fact]
    public async Task TestAsync_OnConnectFailure_ReturnsFalse()
    {
        var conn = new FakeObsConnection
        {
            ConnectHandler = _ => Task.FromException(new InvalidOperationException("接続不可")),
        };
        var tester = new ObsConnectionTester(() => conn);

        var result = await tester.TestAsync(Options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("接続不可", result.Message);
        Assert.Equal(1, conn.DisposeCount);
    }

    [Fact]
    public async Task FetchSourceNamesAsync_OnSuccess_ReturnsNames()
    {
        var conn = new FakeObsConnection
        {
            InputNamesHandler = () => Task.FromResult<IReadOnlyList<string>>(new[] { "INFINITAS", "Mic", "Webcam" }),
        };
        var tester = new ObsConnectionTester(() => conn);

        var sources = await tester.FetchSourceNamesAsync(Options, CancellationToken.None);

        Assert.Equal(3, sources.Count);
        Assert.Contains("INFINITAS", sources);
        Assert.Equal(1, conn.DisposeCount);
    }

    [Fact]
    public async Task FetchSourceNamesAsync_OnConnectFailure_Throws()
    {
        var conn = new FakeObsConnection
        {
            ConnectHandler = _ => Task.FromException(new InvalidOperationException("oops")),
        };
        var tester = new ObsConnectionTester(() => conn);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.FetchSourceNamesAsync(Options, CancellationToken.None));
        Assert.Equal(1, conn.DisposeCount);
    }
}
