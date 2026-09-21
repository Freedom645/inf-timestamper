using InfTimestamper.Core.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Velopack;
using Velopack.Sources;

namespace InfTimestamper.Services;

public sealed class VelopackUpdateService : IUpdateService
{
    public const string DefaultRepositoryUrl = "https://github.com/Freedom645/inf-timestamper";

    private readonly UpdateManager _manager;
    private readonly ILogger<VelopackUpdateService> _logger;
    private UpdateInfo? _pendingUpdate;

    public VelopackUpdateService()
        : this(DefaultRepositoryUrl, includePrerelease: false, NullLogger<VelopackUpdateService>.Instance) { }

    /// <param name="includePrerelease">
    /// GitHub のプレリリースも更新候補に含めるか。α 版を動かしているときだけ true にして、
    /// 正式版の利用者に α 版を配らないようにする。
    /// </param>
    public VelopackUpdateService(string repositoryUrl, bool includePrerelease, ILogger<VelopackUpdateService> logger)
    {
        _logger = logger ?? NullLogger<VelopackUpdateService>.Instance;
        _manager = new UpdateManager(new GithubSource(repositoryUrl, null, includePrerelease));
    }

    public bool IsInstalled => _manager.IsInstalled;

    public async Task<bool> CheckAndDownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (!_manager.IsInstalled)
        {
            _logger.LogInformation("Velopack でインストールされていないため、自動アップデートを適用できません。");
            return false;
        }

        try
        {
            _pendingUpdate = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (_pendingUpdate is null)
            {
                _logger.LogInformation("最新版が利用可能ではありません。");
                return false;
            }

            await _manager.DownloadUpdatesAsync(
                _pendingUpdate,
                pct => progress?.Report(pct),
                cancelToken: cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "アップデートのチェック / ダウンロードに失敗しました。");
            _pendingUpdate = null;
            return false;
        }
    }

    public void ApplyAndRestart()
    {
        if (_pendingUpdate is null) return;
        try
        {
            _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アップデートの適用 / 再起動に失敗しました。");
            throw;
        }
    }
}
