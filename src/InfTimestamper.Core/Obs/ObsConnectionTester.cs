using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.Obs;

public sealed class ObsConnectionTester : IObsConnectionTester
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly Func<IObsConnection> _connectionFactory;
    private readonly ILogger<ObsConnectionTester> _logger;
    private readonly TimeSpan _timeout;

    public ObsConnectionTester(Func<IObsConnection> connectionFactory)
        : this(connectionFactory, NullLogger<ObsConnectionTester>.Instance, DefaultTimeout) { }

    public ObsConnectionTester(
        Func<IObsConnection> connectionFactory,
        ILogger<ObsConnectionTester> logger,
        TimeSpan timeout)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<ObsConnectionTester>.Instance;
        _timeout = timeout;
    }

    public async Task<ObsConnectionTestResult> TestAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var conn = _connectionFactory();
        try
        {
            await conn.ConnectAsync(options, _timeout, cancellationToken).ConfigureAwait(false);
            var info = await conn.GetServerInfoAsync(cancellationToken).ConfigureAwait(false);
            return new ObsConnectionTestResult(true, "接続成功", info.ObsVersion, info.CurrentSceneName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OBS 接続テストに失敗しました。");
            return new ObsConnectionTestResult(false, ex.Message, null, null);
        }
        finally
        {
            try { await conn.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
        }
    }

    public async Task<IReadOnlyList<string>> FetchSourceNamesAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var conn = _connectionFactory();
        try
        {
            await conn.ConnectAsync(options, _timeout, cancellationToken).ConfigureAwait(false);
            return await conn.GetInputNamesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { await conn.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
        }
    }
}
