using InfTimestamper.Core.Obs;

namespace InfTimestamper.Core.Tests.Obs;

public class ObsWebSocketConnectionTests
{
    [Fact]
    public void DecodePngDataUrl_WithStandardPrefix_ReturnsBytes()
    {
        // "INFI" を base64 化
        var expected = System.Text.Encoding.ASCII.GetBytes("INFI");
        var base64 = Convert.ToBase64String(expected);
        var dataUrl = "data:image/png;base64," + base64;

        var bytes = ObsWebSocketConnection.DecodePngDataUrl(dataUrl);

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void DecodePngDataUrl_WithRawBase64_ReturnsBytes()
    {
        var expected = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var base64 = Convert.ToBase64String(expected);

        var bytes = ObsWebSocketConnection.DecodePngDataUrl(base64);

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void DecodePngDataUrl_WithOtherMediaType_StripsHeader()
    {
        var expected = new byte[] { 0x01, 0x02 };
        var base64 = Convert.ToBase64String(expected);
        var dataUrl = "data:image/jpeg;base64," + base64;

        var bytes = ObsWebSocketConnection.DecodePngDataUrl(dataUrl);

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void DecodePngDataUrl_WithEmpty_Throws()
    {
        Assert.Throws<InvalidDataException>(() => ObsWebSocketConnection.DecodePngDataUrl(string.Empty));
    }
}
