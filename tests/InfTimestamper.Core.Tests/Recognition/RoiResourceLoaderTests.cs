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

    [Fact]
    public void Load_BundledInfinitasRois_HasSideAwareKeys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "INFINITAS", "rois.json");
        if (!File.Exists(path)) return; // 同梱されていない環境ではスキップ

        var resource = RoiResourceLoader.Load(path);

        Assert.False(resource.IsEmpty);
        Assert.True(resource.TryGet(RecognitionRoiKeys.LampColor1P, out var lamp1p));
        Assert.True(resource.TryGet(RecognitionRoiKeys.LampColor2P, out var lamp2p));
        Assert.True(resource.TryGet(RecognitionRoiKeys.DifficultyColor, out _));
        Assert.True(resource.TryGet(RecognitionRoiKeys.WithSide(RecognitionFieldKeys.MissCount, PlaySide.OneP), out _));
        Assert.True(resource.TryGet(RecognitionRoiKeys.WithSide(RecognitionFieldKeys.MissCount, PlaySide.TwoP), out _));
        Assert.True(resource.TryGet(RecognitionRoiKeys.WithSide(RecognitionFieldKeys.ExScore, PlaySide.OneP), out _));
        Assert.True(resource.TryGet(RecognitionRoiKeys.WithSide(RecognitionFieldKeys.ExScore, PlaySide.TwoP), out _));

        // 1P と 2P で異なる x 座標を持つことを確認
        Assert.NotEqual(lamp1p.X, lamp2p.X);
        Assert.True(lamp1p.X < lamp2p.X, "1P サイドは画面左、2P サイドは画面右");
    }
}
