using InfTimestamper.Core.YouTube;

namespace InfTimestamper.Core.Tests.YouTube;

public class YouTubeOAuthClientTests
{
    [Fact]
    public void CodeChallenge_MatchesRfc7636Example()
    {
        // RFC 7636 Appendix B の検証ベクタ
        var challenge = YouTubeOAuthClient.CodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void ParseQuery_DecodesRedirectTarget()
    {
        var query = YouTubeOAuthClient.ParseQuery("/?state=abc&code=4%2F0Ab_cd&scope=https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fyoutube.force-ssl");

        Assert.Equal("abc", query["state"]);
        Assert.Equal("4/0Ab_cd", query["code"]);
        Assert.Equal(YouTubeOAuthClient.Scope, query["scope"]);
    }

    [Fact]
    public void ParseQuery_WithoutQuery_ReturnsEmpty()
    {
        Assert.Empty(YouTubeOAuthClient.ParseQuery("/favicon.ico"));
        Assert.Empty(YouTubeOAuthClient.ParseQuery(null));
    }
}
