using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class HashResourceLoaderTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var resource = HashResourceLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.True(resource.IsEmpty);
    }

    [Fact]
    public void LoadFromString_EmptyObject_ReturnsEmpty()
    {
        var resource = HashResourceLoader.LoadFromString("{}");
        Assert.True(resource.IsEmpty);
    }

    [Fact]
    public void LoadFromString_SampleHashes_ParsesAllSections()
    {
        var json = """
        {
          "states": {
            "song_select": [
              { "name": "1p_controller", "roi": [10, 20, 100, 50], "ahash": "0x00ff00ff00ff00ff" },
              { "name": "2p_controller", "roi": [200, 300, 80, 40], "ahash": "ffffffffffffffff", "threshold": 8 }
            ]
          },
          "difficulty": [
            { "value": "SPA", "roi": [500, 100, 60, 30], "ahash": "0x1234567890abcdef" }
          ],
          "dj_level": [
            { "value": "AAA", "roi": [800, 400, 100, 80], "ahash": "0xabcdef1234567890" }
          ],
          "lamp": [
            { "value": "FC", "roi": [900, 500, 120, 40], "ahash": "0xdeadbeefcafebabe" }
          ]
        }
        """;

        var resource = HashResourceLoader.LoadFromString(json);
        Assert.False(resource.IsEmpty);

        var states = resource.States["song_select"];
        Assert.Equal(2, states.Count);
        Assert.Equal("1p_controller", states[0].Name);
        Assert.Equal(new Roi(10, 20, 100, 50), states[0].Roi);
        Assert.Equal(0x00ff00ff00ff00ffUL, states[0].Ahash);
        Assert.Equal(HashResource.DefaultThreshold, states[0].Threshold);
        Assert.Equal(8, states[1].Threshold);
        Assert.Equal(0xffffffffffffffffUL, states[1].Ahash);

        Assert.Equal("SPA", resource.Difficulty[0].Value);
        Assert.Equal(0x1234567890abcdefUL, resource.Difficulty[0].Ahash);

        Assert.Equal("AAA", resource.DjLevel[0].Value);
        Assert.Equal(0xabcdef1234567890UL, resource.DjLevel[0].Ahash);

        Assert.Equal("FC", resource.Lamp[0].Value);
        Assert.Equal(0xdeadbeefcafebabeUL, resource.Lamp[0].Ahash);
    }

    [Fact]
    public void LoadFromString_HashAsNumber_AlsoAccepted()
    {
        var json = """
        { "lamp": [ { "value": "FC", "roi": [0,0,1,1], "ahash": 255 } ] }
        """;
        var resource = HashResourceLoader.LoadFromString(json);
        Assert.Equal(255UL, resource.Lamp[0].Ahash);
    }
}
