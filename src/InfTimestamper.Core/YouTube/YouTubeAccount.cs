using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.YouTube;

/// <summary>
/// ログイン済みアカウントの保存内容。リフレッシュトークンは発行元のクライアントに紐づくため、
/// クライアント ID / シークレットも一緒に持つ（settings.json には書かない）。
/// </summary>
public sealed record YouTubeStoredCredential(
    string ClientId,
    string ClientSecret,
    string RefreshToken,
    string? ChannelTitle);

/// <summary>ログイン情報の保存先。本体では DPAPI で暗号化してファイルに置く。</summary>
public interface IYouTubeCredentialStore
{
    YouTubeStoredCredential? Load();

    void Save(YouTubeStoredCredential credential);

    void Delete();
}

/// <summary>API 呼び出し用のアクセストークンを払い出す。</summary>
public interface IYouTubeAccessTokenSource
{
    bool IsSignedIn { get; }

    /// <param name="forceRefresh">401 を受けた後など、キャッシュを捨てて取り直す場合に true。</param>
    Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
}

/// <summary>
/// YouTube のログイン状態。ログイン / ログアウトと、リフレッシュトークンからのアクセストークン払い出しを受け持つ。
/// </summary>
public sealed class YouTubeAccount : IYouTubeAccessTokenSource
{
    /// <summary>期限ぎりぎりのトークンで API を叩かないための余裕。</summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly IYouTubeOAuthClient _oauth;
    private readonly IYouTubeCredentialStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<YouTubeAccount> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _gate = new();

    private YouTubeStoredCredential? _credential;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public YouTubeAccount(
        IYouTubeOAuthClient oauth,
        IYouTubeCredentialStore store,
        TimeProvider? time = null,
        ILogger<YouTubeAccount>? logger = null)
    {
        _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<YouTubeAccount>.Instance;

        try
        {
            _credential = _store.Load();
        }
        catch (Exception ex)
        {
            // 別ユーザ・別 PC で暗号化されたファイル等。ログインし直せば上書きされる
            _logger.LogWarning(ex, "YouTube のログイン情報を読み込めませんでした。");
        }
    }

    /// <summary>ログイン状態・チャンネル名が変わった。</summary>
    public event EventHandler? Changed;

    public bool IsSignedIn
    {
        get { lock (_gate) return _credential is not null; }
    }

    public string? ChannelTitle
    {
        get { lock (_gate) return _credential?.ChannelTitle; }
    }

    /// <summary>ログイン済みのクライアント ID（設定画面の初期値に使う）。</summary>
    public string? ClientId
    {
        get { lock (_gate) return _credential?.ClientId; }
    }

    public string? ClientSecret
    {
        get { lock (_gate) return _credential?.ClientSecret; }
    }

    /// <summary>ブラウザで同意画面を開いてログインする。成功すると資格情報を保存する。</summary>
    public async Task SignInAsync(YouTubeClientCredentials credentials, CancellationToken cancellationToken)
    {
        if (credentials is null) throw new ArgumentNullException(nameof(credentials));

        var token = await _oauth.AuthorizeAsync(credentials, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new YouTubeAuthException("リフレッシュトークンを受け取れませんでした。");

        var credential = new YouTubeStoredCredential(
            credentials.ClientId, credentials.ClientSecret, token.RefreshToken, ChannelTitle: null);
        _store.Save(credential);
        lock (_gate)
        {
            _credential = credential;
            _accessToken = token.AccessToken;
            _accessTokenExpiresAt = token.ExpiresAt;
        }
        _logger.LogInformation("YouTube にログインしました。");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>表示用のチャンネル名を保存する。</summary>
    public void SetChannelTitle(string? title)
    {
        YouTubeStoredCredential updated;
        lock (_gate)
        {
            if (_credential is null) return;
            updated = _credential with { ChannelTitle = title };
            _credential = updated;
        }
        _store.Save(updated);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>トークンを失効させて保存内容を消す。失効に失敗してもローカルの情報は消す。</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        YouTubeStoredCredential? credential;
        lock (_gate)
        {
            credential = _credential;
            _credential = null;
            _accessToken = null;
        }
        if (credential is null) return;

        try
        {
            await _oauth.RevokeAsync(credential.RefreshToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube のトークン失効に失敗しました（ローカルのログイン情報は削除します）。");
        }

        _store.Delete();
        _logger.LogInformation("YouTube からログアウトしました。");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            YouTubeStoredCredential credential;
            lock (_gate)
            {
                credential = _credential ?? throw new YouTubeAuthException("YouTube にログインしていません。", requiresSignIn: true);
                if (!forceRefresh && _accessToken is not null
                    && _time.GetUtcNow() < _accessTokenExpiresAt - ExpiryMargin)
                {
                    return _accessToken;
                }
            }

            var token = await _oauth.RefreshAsync(
                new YouTubeClientCredentials(credential.ClientId, credential.ClientSecret),
                credential.RefreshToken,
                cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                // 更新中にログアウトされていたら結果を捨てる
                if (_credential?.RefreshToken != credential.RefreshToken)
                    throw new YouTubeAuthException("YouTube からログアウトされました。", requiresSignIn: true);
                _accessToken = token.AccessToken;
                _accessTokenExpiresAt = token.ExpiresAt;
            }
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
