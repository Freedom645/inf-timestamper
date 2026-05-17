namespace InfTimestamper.Core.Obs;

public sealed class ObsConnectionPool : IAsyncDisposable
{
    private readonly bool _shared;
    private bool _disposed;

    private ObsConnectionPool(IObsConnection stream, IObsConnection capture, bool shared)
    {
        StreamConnection = stream;
        CaptureConnection = capture;
        _shared = shared;
    }

    public IObsConnection StreamConnection { get; }
    public IObsConnection CaptureConnection { get; }
    public bool IsTwoPcConfiguration => !_shared;

    public static ObsConnectionPool CreateSinglePc(Func<IObsConnection> factory)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        var shared = factory() ?? throw new InvalidOperationException("接続ファクトリが null を返しました。");
        return new ObsConnectionPool(shared, shared, shared: true);
    }

    public static ObsConnectionPool CreateTwoPc(
        Func<IObsConnection> streamFactory,
        Func<IObsConnection> captureFactory)
    {
        if (streamFactory is null) throw new ArgumentNullException(nameof(streamFactory));
        if (captureFactory is null) throw new ArgumentNullException(nameof(captureFactory));

        var stream = streamFactory() ?? throw new InvalidOperationException("Stream 用接続ファクトリが null を返しました。");
        var capture = captureFactory() ?? throw new InvalidOperationException("Capture 用接続ファクトリが null を返しました。");
        if (ReferenceEquals(stream, capture))
            throw new InvalidOperationException("2 台 PC 構成では Stream と Capture に異なる接続インスタンスを渡す必要があります。");

        return new ObsConnectionPool(stream, capture, shared: false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StreamConnection.DisposeAsync().ConfigureAwait(false);
        if (!_shared)
            await CaptureConnection.DisposeAsync().ConfigureAwait(false);
    }
}
