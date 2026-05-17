using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

public class ObsConnectionPoolTests
{
    [Fact]
    public void CreateSinglePc_ReturnsSameInstanceForBothRoles()
    {
        var conn = new FakeObsConnection();
        using var pool = ToSyncDisposable(ObsConnectionPool.CreateSinglePc(() => conn));

        Assert.Same(conn, pool.Pool.StreamConnection);
        Assert.Same(conn, pool.Pool.CaptureConnection);
        Assert.False(pool.Pool.IsTwoPcConfiguration);
    }

    [Fact]
    public void CreateTwoPc_ReturnsDifferentInstances()
    {
        var stream = new FakeObsConnection();
        var capture = new FakeObsConnection();

        using var pool = ToSyncDisposable(ObsConnectionPool.CreateTwoPc(() => stream, () => capture));

        Assert.Same(stream, pool.Pool.StreamConnection);
        Assert.Same(capture, pool.Pool.CaptureConnection);
        Assert.True(pool.Pool.IsTwoPcConfiguration);
    }

    [Fact]
    public void CreateTwoPc_WithSameInstance_Throws()
    {
        var shared = new FakeObsConnection();
        Assert.Throws<InvalidOperationException>(
            () => ObsConnectionPool.CreateTwoPc(() => shared, () => shared));
    }

    [Fact]
    public async Task DisposeAsync_SinglePc_DisposesSharedOnce()
    {
        var conn = new FakeObsConnection();
        var pool = ObsConnectionPool.CreateSinglePc(() => conn);
        await pool.DisposeAsync();

        Assert.Equal(1, conn.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_TwoPc_DisposesBoth()
    {
        var stream = new FakeObsConnection();
        var capture = new FakeObsConnection();
        var pool = ObsConnectionPool.CreateTwoPc(() => stream, () => capture);
        await pool.DisposeAsync();

        Assert.Equal(1, stream.DisposeCount);
        Assert.Equal(1, capture.DisposeCount);
    }

    private static PoolHolder ToSyncDisposable(ObsConnectionPool pool) => new(pool);

    private readonly struct PoolHolder : IDisposable
    {
        public PoolHolder(ObsConnectionPool pool) { Pool = pool; }
        public ObsConnectionPool Pool { get; }
        public void Dispose() => Pool.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
