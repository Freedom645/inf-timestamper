using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class RoiResourceLoaderTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var resource = RoiResourceLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.True(resource.IsEmpty);
    }

    [Fact]
    public void LoadFromString_SampleRois_ParsesAll()
    {
        var json = """
        {
          "title":      [600, 100, 720, 40],
          "miss_count": [1500, 800, 120, 50],
          "ex_score":   [1200, 800, 200, 50],
          "level":      [800, 110, 80, 30]
        }
        """;

        var resource = RoiResourceLoader.LoadFromString(json);
        Assert.Equal(4, resource.Rois.Count);
        Assert.True(resource.TryGet("title", out var title));
        Assert.Equal(new Roi(600, 100, 720, 40), title);
        Assert.True(resource.TryGet("miss_count", out var miss));
        Assert.Equal(new Roi(1500, 800, 120, 50), miss);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var resource = RoiResourceLoader.LoadFromString("""{ "title": [0,0,1,1] }""");
        Assert.False(resource.TryGet("missing", out _));
    }
}
