using InfTimestamper.Core.YouTube;
using Microsoft.Extensions.Time.Testing;

namespace InfTimestamper.Core.Tests.YouTube;

public class YouTubeAccountTests
{
    private static readonly YouTubeClientCredentials Credentials = new("client-id", "client-secret");

    private sealed class FakeOAuthClient : IYouTubeOAuthClient
    {
        private readonly FakeTimeProvider _time;
        public FakeOAuthClient(FakeTimeProvider time) => _time = time;

        public int RefreshCalls { get; private set; }
        public List<string> Revoked { get; } = new();
        public Exception? RefreshError { get; set; }

        public Task<YouTubeTokenResponse> AuthorizeAsync(YouTubeClientCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new YouTubeTokenResponse("access-0", _time.GetUtcNow().AddHours(1), "refresh-token"));

        public Task<YouTubeTokenResponse> RefreshAsync(YouTubeClientCredentials credentials, string refreshToken, CancellationToken cancellationToken)
        {
            if (RefreshError is not null) throw RefreshError;
            RefreshCalls++;
            return Task.FromResult(new YouTubeTokenResponse($"access-{RefreshCalls}", _time.GetUtcNow().AddHours(1), null));
        }

        public Task RevokeAsync(string token, CancellationToken cancellationToken)
        {
            Revoked.Add(token);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStore : IYouTubeCredentialStore
    {
        public YouTubeStoredCredential? Stored { get; set; }
        public bool ThrowOnLoad { get; set; }

        public YouTubeStoredCredential? Load()
            => ThrowOnLoad ? throw new InvalidOperationException("復号できない") : Stored;

        public void Save(YouTubeStoredCredential credential) => Stored = credential;
        public void Delete() => Stored = null;
    }

    [Fact]
    public async Task SignIn_StoresCredentialAndUsesIssuedToken()
    {
        var time = new FakeTimeProvider();
        var oauth = new FakeOAuthClient(time);
        var store = new InMemoryStore();
        var account = new YouTubeAccount(oauth, store, time);

        await account.SignInAsync(Credentials, CancellationToken.None);

        Assert.True(account.IsSignedIn);
        Assert.Equal("refresh-token", store.Stored?.RefreshToken);
        Assert.Equal("client-id", store.Stored?.ClientId);
        Assert.Equal("access-0", await account.GetAccessTokenAsync(false, CancellationToken.None));
        Assert.Equal(0, oauth.RefreshCalls);
    }

    [Fact]
    public async Task ExpiredToken_IsRefreshed()
    {
        var time = new FakeTimeProvider();
        var oauth = new FakeOAuthClient(time);
        var account = new YouTubeAccount(oauth, new InMemoryStore(), time);
        await account.SignInAsync(Credentials, CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(59.5));

        Assert.Equal("access-1", await account.GetAccessTokenAsync(false, CancellationToken.None));
    }

    [Fact]
    public async Task StoredCredential_IsUsedAfterRestart()
    {
        var time = new FakeTimeProvider();
        var store = new InMemoryStore
        {
            Stored = new YouTubeStoredCredential("client-id", "client-secret", "refresh-token", "チャンネル"),
        };
        var account = new YouTubeAccount(new FakeOAuthClient(time), store, time);

        Assert.True(account.IsSignedIn);
        Assert.Equal("チャンネル", account.ChannelTitle);
        Assert.Equal("access-1", await account.GetAccessTokenAsync(false, CancellationToken.None));
    }

    [Fact]
    public async Task SignOut_RevokesAndDeletes()
    {
        var time = new FakeTimeProvider();
        var oauth = new FakeOAuthClient(time);
        var store = new InMemoryStore();
        var account = new YouTubeAccount(oauth, store, time);
        await account.SignInAsync(Credentials, CancellationToken.None);

        await account.SignOutAsync(CancellationToken.None);

        Assert.False(account.IsSignedIn);
        Assert.Null(store.Stored);
        Assert.Equal(new[] { "refresh-token" }, oauth.Revoked);
        var ex = await Assert.ThrowsAsync<YouTubeAuthException>(
            () => account.GetAccessTokenAsync(false, CancellationToken.None));
        Assert.True(ex.RequiresSignIn);
    }

    [Fact]
    public void UnreadableStore_IsTreatedAsSignedOut()
    {
        var time = new FakeTimeProvider();
        var account = new YouTubeAccount(new FakeOAuthClient(time), new InMemoryStore { ThrowOnLoad = true }, time);

        Assert.False(account.IsSignedIn);
    }
}
