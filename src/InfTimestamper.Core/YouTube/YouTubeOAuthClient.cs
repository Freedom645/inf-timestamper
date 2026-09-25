using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfTimestamper.Core.YouTube;

/// <summary>利用者が Google Cloud で作成した OAuth クライアント（種類「デスクトップ アプリ」）。</summary>
public sealed record YouTubeClientCredentials(string ClientId, string ClientSecret);

/// <summary>トークンエンドポイントの応答。<see cref="RefreshToken"/> は認可コードの交換時にだけ入る。</summary>
public sealed record YouTubeTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string? RefreshToken);

public interface IYouTubeOAuthClient
{
    /// <summary>ブラウザで同意画面を開き、認可コードをトークンへ交換する。</summary>
    Task<YouTubeTokenResponse> AuthorizeAsync(YouTubeClientCredentials credentials, CancellationToken cancellationToken);

    Task<YouTubeTokenResponse> RefreshAsync(YouTubeClientCredentials credentials, string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string token, CancellationToken cancellationToken);
}

/// <summary>
/// デスクトップアプリ向けの OAuth 2.0（ループバックリダイレクト + PKCE）。
/// リダイレクト先は <c>http://127.0.0.1:{空きポート}/</c> で、HTTP.sys（URL ACL）を避けるため
/// <see cref="TcpListener"/> で 1 リクエストだけ受ける。
/// </summary>
public sealed class YouTubeOAuthClient : IYouTubeOAuthClient
{
    /// <summary>概要欄の書き換え（<c>videos.update</c>）に必要なスコープ。ライブの一覧取得もこれで足りる。</summary>
    public const string Scope = "https://www.googleapis.com/auth/youtube.force-ssl";

    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    private readonly HttpClient _http;
    private readonly Action<string> _openBrowser;
    private readonly ILogger<YouTubeOAuthClient> _logger;

    public YouTubeOAuthClient(HttpClient http, Action<string> openBrowser, ILogger<YouTubeOAuthClient>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _openBrowser = openBrowser ?? throw new ArgumentNullException(nameof(openBrowser));
        _logger = logger ?? NullLogger<YouTubeOAuthClient>.Instance;
    }

    public async Task<YouTubeTokenResponse> AuthorizeAsync(
        YouTubeClientCredentials credentials,
        CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirectUri = $"http://127.0.0.1:{port}/";
            var verifier = RandomBase64Url(48);
            var state = RandomBase64Url(16);

            var url = AuthorizationEndpoint
                + "?client_id=" + Uri.EscapeDataString(credentials.ClientId)
                + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                + "&response_type=code"
                + "&scope=" + Uri.EscapeDataString(Scope)
                + "&code_challenge=" + CodeChallenge(verifier)
                + "&code_challenge_method=S256"
                // リフレッシュトークンを確実に受け取るため、毎回同意画面を出す
                + "&access_type=offline&prompt=consent"
                + "&state=" + state;
            _openBrowser(url);

            var query = await ReceiveRedirectAsync(listener, cancellationToken).ConfigureAwait(false);
            if (query.TryGetValue("error", out var error))
                throw new YouTubeAuthException($"認可されませんでした（{error}）。");
            if (!query.TryGetValue("state", out var returnedState) || returnedState != state)
                throw new YouTubeAuthException("認可の応答が不正です（state 不一致）。");
            if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
                throw new YouTubeAuthException("認可コードを受け取れませんでした。");

            return await RequestTokenAsync(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier,
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    public Task<YouTubeTokenResponse> RefreshAsync(
        YouTubeClientCredentials credentials,
        string refreshToken,
        CancellationToken cancellationToken)
        => RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
        using var response = await _http.PostAsync(RevokeEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("トークンの失効に失敗しました（HTTP {Status}）。", (int)response.StatusCode);
    }

    private async Task<YouTubeTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        TokenEndpointResponse? body;
        try
        {
            body = JsonSerializer.Deserialize<TokenEndpointResponse>(raw);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (!response.IsSuccessStatusCode || body?.AccessToken is null)
        {
            var error = body?.Error ?? $"HTTP {(int)response.StatusCode}";
            // invalid_grant はリフレッシュトークンの失効・取り消し。再ログインが必要
            throw new YouTubeAuthException(
                $"トークンを取得できませんでした（{error}）。",
                requiresSignIn: error == "invalid_grant");
        }

        return new YouTubeTokenResponse(
            body.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn),
            body.RefreshToken);
    }

    /// <summary>ブラウザからのリダイレクトを 1 件受け、クエリ文字列を返す。</summary>
    private static async Task<Dictionary<string, string>> ReceiveRedirectAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            // 1 行目だけ読めば十分（"GET /?code=...&state=... HTTP/1.1"）
            var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var target = requestLine?.Split(' ') is { Length: >= 2 } parts ? parts[1] : null;
            var query = ParseQuery(target);

            // favicon など、認可の応答でないリクエストは読み捨てて待ち続ける
            var isRedirect = query.ContainsKey("code") || query.ContainsKey("error");
            var html = isRedirect
                ? "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>INF-TIMESTAMPER</title></head>"
                  + "<body><p>INF-TIMESTAMPER: 認可の処理が終わりました。このタブは閉じてかまいません。</p></body></html>"
                : string.Empty;
            var payload = Encoding.UTF8.GetBytes(html);
            var header = Encoding.ASCII.GetBytes(
                (isRedirect ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 404 Not Found\r\n")
                + "Content-Type: text/html; charset=utf-8\r\n"
                + $"Content-Length: {payload.Length}\r\n"
                + "Connection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

            if (isRedirect) return query;
        }
    }

    internal static Dictionary<string, string> ParseQuery(string? target)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(target)) return result;

        var index = target.IndexOf('?');
        if (index < 0) return result;

        foreach (var pair in target[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            var value = eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result[key] = value;
        }
        return result;
    }

    internal static string CodeChallenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string RandomBase64Url(int byteCount)
        => Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}

/// <summary>認可・トークン取得の失敗。</summary>
public sealed class YouTubeAuthException : Exception
{
    public YouTubeAuthException(string message, bool requiresSignIn = false) : base(message)
    {
        RequiresSignIn = requiresSignIn;
    }

    /// <summary>リフレッシュトークンが使えなくなっており、ログインし直す必要がある。</summary>
    public bool RequiresSignIn { get; }
}
