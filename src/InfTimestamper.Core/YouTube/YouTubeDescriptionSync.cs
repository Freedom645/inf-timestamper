using System.Text.Json.Nodes;
using InfTimestamper.Core.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.YouTube;

/// <summary>概要欄の同期の設定。</summary>
/// <param name="Heading">タイムスタンプのブロックの見出し行（管理範囲の目印）。</param>
/// <param name="MinInterval">概要欄を書き換える最短間隔。<c>videos.update</c> は 1 回 50 ユニットと重いため間引く。</param>
/// <param name="MatchTolerance">記録の配信開始時間とライブの開始時刻の許容差。</param>
public sealed record YouTubeSyncOptions(string Heading, TimeSpan MinInterval, TimeSpan MatchTolerance);

public enum YouTubeSyncState
{
    /// <summary>同期していない（機能 OFF・未ログイン・記録中でない）。</summary>
    Inactive,

    /// <summary>対象のライブを探している。</summary>
    Searching,

    /// <summary>対象のライブが見つかり、概要欄を更新している。</summary>
    Linked,

    /// <summary>対象のライブが見つからなかった（この記録では同期しない）。</summary>
    NotFound,

    /// <summary>直近の更新に失敗した（次の機会に再試行する）。</summary>
    Error,

    /// <summary>API の 1 日の上限に達したため、この記録では同期をやめた。</summary>
    QuotaExceeded,

    /// <summary>ログインし直す必要がある（リフレッシュトークンの失効等）。</summary>
    SignInRequired,
}

public sealed record YouTubeSyncStatus(
    YouTubeSyncState State,
    string? BroadcastTitle = null,
    DateTimeOffset? LastUpdatedAt = null)
{
    public static readonly YouTubeSyncStatus Inactive = new(YouTubeSyncState.Inactive);
}

/// <summary>
/// 記録中のタイムスタンプを、記録の配信開始時間とほぼ同時に始まったライブの概要欄へ書き込み続ける。
/// <list type="bullet">
/// <item>記録開始（<see cref="BeginSession"/>）で配信中のライブを探し、開始時刻が最も近いものを対象にする。
///   YouTube 側で配信中になるまで少し遅れるため、許容差の間は一定間隔で探し直す</item>
/// <item>更新の依頼（<see cref="RequestUpdate"/>）は最新の内容だけを保持し、
///   <see cref="YouTubeSyncOptions.MinInterval"/> の間隔で間引いて書き込む</item>
/// <item>記録終了（<see cref="EndSessionAsync"/>）では間隔を待たずに最終版を書き込む</item>
/// </list>
/// 失敗しても配信の記録には影響させず、ダイアログも出さない（状態を <see cref="StatusChanged"/> で通知するのみ）。
/// </summary>
public sealed class YouTubeDescriptionSync : IAsyncDisposable
{
    /// <summary>ライブが見つからないときに探し直す間隔。</summary>
    public static readonly TimeSpan SearchRetryInterval = TimeSpan.FromSeconds(30);

    /// <summary>記録終了時の最終書き込みを待つ上限。</summary>
    public static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(30);

    private readonly IYouTubeApi _api;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly ILogger<YouTubeDescriptionSync> _logger;
    private readonly object _gate = new();

    private Session? _session;
    private YouTubeSyncStatus _status = YouTubeSyncStatus.Inactive;

    public YouTubeDescriptionSync(
        IYouTubeApi api,
        IUiDispatcher? dispatcher = null,
        TimeProvider? time = null,
        ILogger<YouTubeDescriptionSync>? logger = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<YouTubeDescriptionSync>.Instance;
    }

    /// <summary>同期の状態が変わった。UI スレッドで通知する。</summary>
    public event EventHandler<YouTubeSyncStatus>? StatusChanged;

    public YouTubeSyncStatus Status
    {
        get { lock (_gate) return _status; }
    }

    /// <summary>同期のセッションが動いているか。</summary>
    public bool IsActive
    {
        get { lock (_gate) return _session is not null; }
    }

    /// <summary>ログイン済みで同期を始められるか。</summary>
    public bool IsAvailable => _api.IsAvailable;

    /// <summary>
    /// 同期を始める。既にセッションがあれば（最終書き込みをせずに）打ち切って始め直す。
    /// 未ログインなら何もしない。
    /// </summary>
    public void BeginSession(DateTimeOffset streamStartedAt, YouTubeSyncOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (!_api.IsAvailable)
        {
            _logger.LogInformation("YouTube にログインしていないため、概要欄の同期を行いません。");
            return;
        }

        Session session;
        Session? previous;
        lock (_gate)
        {
            previous = _session;
            session = new Session(streamStartedAt, options);
            _session = session;
        }
        previous?.Cancel();

        _logger.LogInformation("YouTube の概要欄の同期を開始します（配信開始時間 {StartedAt:yyyy-MM-dd HH:mm:ss}）。",
            streamStartedAt.ToLocalTime());
        session.Loop = Task.Run(() => RunAsync(session));
    }

    /// <summary>見出し行・更新間隔の変更を反映する（次の書き込みから効く）。</summary>
    public void UpdateOptions(YouTubeSyncOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        lock (_gate)
        {
            if (_session is null) return;
            _session.Options = options;
        }
    }

    /// <summary>概要欄に書くタイムスタンプ行を更新する。書き込みは間引いて行う。</summary>
    public void RequestUpdate(IReadOnlyList<string> lines)
    {
        if (lines is null) throw new ArgumentNullException(nameof(lines));
        Session? session;
        lock (_gate) session = _session;
        session?.SetPending(lines.ToList());
    }

    /// <summary>
    /// 同期を終える。<paramref name="finalLines"/> を渡すと、間隔を待たずに最終版を書き込んでから終える
    /// （<see cref="FlushTimeout"/> を超えたら打ち切る）。
    /// </summary>
    public async Task EndSessionAsync(IReadOnlyList<string>? finalLines)
    {
        Session? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }
        if (session is null) return;

        if (finalLines is not null)
        {
            session.SetPending(finalLines.ToList());
            session.RequestFlush();
            var loop = session.Loop ?? Task.CompletedTask;
            var finished = await Task.WhenAny(loop, Task.Delay(FlushTimeout, _time)).ConfigureAwait(false);
            if (finished != loop)
                _logger.LogWarning("YouTube の概要欄の最終書き込みが {Timeout} 秒以内に終わらなかったため打ち切ります。",
                    FlushTimeout.TotalSeconds);
        }

        session.Cancel();
        _logger.LogInformation("YouTube の概要欄の同期を終了しました。");
    }

    public async ValueTask DisposeAsync()
    {
        Session? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }
        if (session is null) return;

        session.Cancel();
        if (session.Loop is not null)
        {
            try { await session.Loop.ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogDebug(ex, "YouTube 同期ループの停止中に例外が発生しました。"); }
        }
    }

    private async Task RunAsync(Session session)
    {
        try
        {
            var broadcast = await FindBroadcastAsync(session).ConfigureAwait(false);
            if (broadcast is null) return;

            while (true)
            {
                await session.WaitForWorkAsync().ConfigureAwait(false);

                if (!session.IsFlushRequested)
                {
                    // 前回の書き込みから MinInterval 経つまで待つ（記録終了の最終書き込みでは待たない）
                    var wait = session.LastAttemptAt + session.Options.MinInterval - _time.GetUtcNow();
                    if (wait > TimeSpan.Zero)
                        await session.DelayUnlessFlushAsync(wait, _time).ConfigureAwait(false);
                }

                var lines = session.TakePending();
                if (lines is not null && !await WriteAsync(session, broadcast, lines).ConfigureAwait(false))
                    return;

                if (session.IsFlushRequested && !session.HasPending)
                    return;
            }
        }
        catch (OperationCanceledException) when (session.IsCanceled)
        {
            // 同期の打ち切り
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube の概要欄の同期中に予期しないエラーが発生しました。");
            SetStatus(session, YouTubeSyncStatus.Inactive with { State = YouTubeSyncState.Error });
        }
    }

    /// <summary>対象のライブを探す。見つからないまま許容差を過ぎたら null。</summary>
    private async Task<YouTubeBroadcast?> FindBroadcastAsync(Session session)
    {
        SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.Searching));

        while (true)
        {
            try
            {
                var broadcasts = await _api.ListActiveBroadcastsAsync(session.Token).ConfigureAwait(false);
                var match = YouTubeBroadcastMatcher.FindClosest(
                    broadcasts, session.StreamStartedAt, session.Options.MatchTolerance);
                if (match is not null)
                {
                    _logger.LogInformation("YouTube のライブ「{Title}」（{Id}）の概要欄を同期します。", match.Title, match.Id);
                    SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.Linked, match.Title));
                    return match;
                }
                _logger.LogDebug("配信開始時間に対応する YouTube のライブが見つかりません（配信中 {Count} 件）。", broadcasts.Count);
            }
            catch (Exception ex) when (TryStop(session, ex))
            {
                return null;
            }
            catch (Exception ex) when (!session.IsCanceled)
            {
                _logger.LogWarning(ex, "YouTube のライブ一覧の取得に失敗しました。");
            }

            // 記録開始から許容差を過ぎても見つからなければ、この記録のライブは無いものとする
            var deadline = session.StreamStartedAt + session.Options.MatchTolerance;
            if (session.IsFlushRequested || _time.GetUtcNow() >= deadline)
            {
                _logger.LogInformation("配信開始時間に対応する YouTube のライブが見つからないため、概要欄を同期しません。");
                SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.NotFound));
                return null;
            }

            await session.DelayUnlessFlushAsync(SearchRetryInterval, _time).ConfigureAwait(false);
        }
    }

    /// <summary>概要欄を書き込む。同期を続けられない失敗なら false。</summary>
    private async Task<bool> WriteAsync(Session session, YouTubeBroadcast broadcast, IReadOnlyList<string> lines)
    {
        session.LastAttemptAt = _time.GetUtcNow();
        try
        {
            var snippet = await _api.GetVideoSnippetAsync(broadcast.Id, session.Token).ConfigureAwait(false);
            if (snippet is null)
            {
                _logger.LogWarning("YouTube の動画 {Id} が見つかりません（削除された可能性があります）。", broadcast.Id);
                SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.NotFound));
                return false;
            }

            var existing = snippet["description"]?.GetValue<string>() ?? string.Empty;
            var composed = YouTubeDescriptionComposer.Compose(existing, session.Options.Heading, lines, out var dropped);
            if (dropped > 0)
                _logger.LogWarning("概要欄の上限（{Max} バイト）を超えるため、タイムスタンプを {Dropped} 行省略しました。",
                    YouTubeDescriptionComposer.MaxDescriptionBytes, dropped);

            if (composed != existing)
            {
                await _api.UpdateVideoSnippetAsync(broadcast.Id, BuildUpdateSnippet(snippet, composed), session.Token)
                    .ConfigureAwait(false);
                _logger.LogInformation("YouTube の概要欄を更新しました（タイムスタンプ {Count} 行）。", lines.Count);
            }

            SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.Linked, broadcast.Title, _time.GetUtcNow()));
            return true;
        }
        catch (Exception ex) when (TryStop(session, ex))
        {
            return false;
        }
        catch (Exception ex) when (!session.IsCanceled)
        {
            // 一時的な失敗。新しい依頼が無ければ同じ内容を次の間隔で書き直す
            _logger.LogWarning(ex, "YouTube の概要欄の更新に失敗しました。次の更新で再試行します。");
            session.RestorePending(lines);
            SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.Error, broadcast.Title, Status.LastUpdatedAt));
            return !session.IsFlushRequested;
        }
    }

    /// <summary>
    /// <c>videos.update</c> は snippet を丸ごと置き換える（送らなかったタグ等は消える）ので、
    /// 書き換え可能な項目は取得した値をそのまま送り返す。
    /// </summary>
    internal static JsonObject BuildUpdateSnippet(JsonObject current, string description)
    {
        var snippet = new JsonObject();
        foreach (var key in new[] { "title", "categoryId", "tags", "defaultLanguage", "defaultAudioLanguage" })
        {
            if (current[key] is { } value)
                snippet[key] = value.DeepClone();
        }
        snippet["description"] = description;
        return snippet;
    }

    /// <summary>同期を続けても無駄な失敗（クォータ超過・要再ログイン）なら状態を出して true。</summary>
    private bool TryStop(Session session, Exception ex)
    {
        if (ex is YouTubeApiException { IsQuotaExceeded: true })
        {
            _logger.LogWarning(ex, "YouTube API の 1 日の上限に達したため、この記録では概要欄の同期をやめます。");
            SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.QuotaExceeded));
            return true;
        }
        if (ex is YouTubeAuthException { RequiresSignIn: true })
        {
            _logger.LogWarning(ex, "YouTube にログインし直す必要があります。");
            SetStatus(session, new YouTubeSyncStatus(YouTubeSyncState.SignInRequired));
            return true;
        }
        return false;
    }

    private void SetStatus(Session session, YouTubeSyncStatus status)
    {
        lock (_gate)
        {
            // 打ち切られたセッションの状態で新しいセッションの表示を上書きしない
            if (session.IsCanceled && !ReferenceEquals(_session, session)) return;
            if (_status == status) return;
            _status = status;
        }
        _dispatcher.Invoke(() => StatusChanged?.Invoke(this, status));
    }

    private sealed class Session
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly CancellationTokenSource _flushCts = new();
        private readonly SemaphoreSlim _work = new(0, 1);
        private readonly object _gate = new();
        private List<string>? _pending;
        private List<string>? _lastWritten;

        public Session(DateTimeOffset streamStartedAt, YouTubeSyncOptions options)
        {
            StreamStartedAt = streamStartedAt;
            Options = options;
        }

        public DateTimeOffset StreamStartedAt { get; }
        public volatile YouTubeSyncOptions Options;
        public Task? Loop { get; set; }
        public DateTimeOffset LastAttemptAt { get; set; } = DateTimeOffset.MinValue;
        public CancellationToken Token => _cts.Token;
        public bool IsCanceled => _cts.IsCancellationRequested;
        public bool IsFlushRequested => _flushCts.IsCancellationRequested;

        public bool HasPending
        {
            get { lock (_gate) return _pending is not null; }
        }

        public void SetPending(List<string> lines)
        {
            lock (_gate)
            {
                // 最後に書いた内容と同じなら書き込む必要が無い
                if (_lastWritten is not null && _lastWritten.SequenceEqual(lines))
                {
                    _pending = null;
                    return;
                }
                _pending = lines;
            }
            Signal();
        }

        /// <summary>書き込みに失敗した内容を戻す（その間に新しい依頼が来ていればそちらを優先）。</summary>
        public void RestorePending(IReadOnlyList<string> lines)
        {
            lock (_gate)
            {
                _lastWritten = null;
                _pending ??= lines.ToList();
            }
            Signal();
        }

        public List<string>? TakePending()
        {
            lock (_gate)
            {
                var lines = _pending;
                _pending = null;
                if (lines is not null) _lastWritten = lines;
                return lines;
            }
        }

        public async Task WaitForWorkAsync()
        {
            if (IsFlushRequested) return;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _flushCts.Token);
                await _work.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsFlushRequested && !IsCanceled)
            {
            }
        }

        /// <summary>待つ。最終書き込みが依頼されたら即座に戻る。</summary>
        public async Task DelayUnlessFlushAsync(TimeSpan delay, TimeProvider time)
        {
            if (IsFlushRequested) return;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _flushCts.Token);
                await Task.Delay(delay, time, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (IsFlushRequested && !IsCanceled)
            {
            }
        }

        public void RequestFlush()
        {
            try { _flushCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        public void Cancel()
        {
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private void Signal()
        {
            lock (_gate)
            {
                if (_work.CurrentCount == 0)
                    _work.Release();
            }
        }
    }
}
