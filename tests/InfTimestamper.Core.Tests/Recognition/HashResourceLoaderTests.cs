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
        Assert.Equal(0x1234567890abcdefUL, resource.Difficulty[0].Hash);

        Assert.Equal("AAA", resource.DjLevel[0].Value);
        Assert.Equal(0xabcdef1234567890UL, resource.DjLevel[0].Hash);

        Assert.Equal("FC", resource.Lamp[0].Value);
        Assert.Equal(0xdeadbeefcafebabeUL, resource.Lamp[0].Hash);
    }

    [Fact]
    public void LoadFromString_HashAsNumber_AlsoAccepted()
    {
        var json = """
        { "lamp": [ { "value": "FC", "roi": [0,0,1,1], "ahash": 255 } ] }
        """;
        var resource = HashResourceLoader.LoadFromString(json);
        Assert.Equal(255UL, resource.Lamp[0].Hash);
    }

    [Fact]
    public void LoadFromString_PhashFieldSetsPerceptualAlgo()
    {
        var json = """
        {
          "dj_level": [
            { "value": "AAA", "roi": [10,20,30,40], "phash": "0xfd627e62760009be", "threshold": 8 }
          ]
        }
        """;

        var resource = HashResourceLoader.LoadFromString(json);
        var entry = resource.DjLevel[0];

        Assert.Equal(0xfd627e62760009beUL, entry.Hash);
        Assert.Equal(HashAlgorithm.Perceptual, entry.Algo);
        Assert.Equal(8, entry.Threshold);
    }

    [Fact]
    public void LoadFromString_SideField_ParsesPlaySide()
    {
        var json = """
        {
          "dj_level": [
            { "value": "AAA", "side": "1p", "roi": [10,20,30,40], "ahash": "0x1" },
            { "value": "AAA", "side": "2p", "roi": [10,20,30,40], "ahash": "0x2" },
            { "value": "AAA", "roi": [10,20,30,40], "ahash": "0x3" }
          ]
        }
        """;

        var resource = HashResourceLoader.LoadFromString(json);
        Assert.Equal(PlaySide.OneP, resource.DjLevel[0].Side);
        Assert.Equal(PlaySide.TwoP, resource.DjLevel[1].Side);
        Assert.Equal(PlaySide.Unknown, resource.DjLevel[2].Side);
    }

    [Fact]
    public void LoadFromString_PlayModeSection_ParsesEntries()
    {
        var json = """
        {
          "play_mode": [
            { "value": "SP", "roi": [920,1035,60,40], "phash": "0xbf281aa60f1bb4c3", "threshold": 10 },
            { "value": "DP", "roi": [920,1035,60,40], "phash": "0x8037ec4fb0f04f8d", "threshold": 10 }
          ]
        }
        """;

        var resource = HashResourceLoader.LoadFromString(json);
        Assert.Equal(2, resource.PlayMode.Count);
        Assert.Equal("SP", resource.PlayMode[0].Value);
        Assert.Equal("DP", resource.PlayMode[1].Value);
        Assert.All(resource.PlayMode, e => Assert.Equal(HashAlgorithm.Perceptual, e.Algo));
    }

    [Fact]
    public void Load_BundledInfinitasHashes_HasDjLevelAndPlayMode()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "INFINITAS", "hashes.json");
        if (!File.Exists(path)) return; // 同梱されていない環境ではスキップ

        var resource = HashResourceLoader.Load(path);

        // DJ Level: 1P/2P それぞれ 8 文字 = 16 エントリ
        Assert.Equal(16, resource.DjLevel.Count);
        var letters1P = resource.DjLevel.Where(e => e.Side == PlaySide.OneP).Select(e => e.Value).OrderBy(v => v).ToArray();
        var letters2P = resource.DjLevel.Where(e => e.Side == PlaySide.TwoP).Select(e => e.Value).OrderBy(v => v).ToArray();
        Assert.Equal(new[] { "A", "AA", "AAA", "B", "C", "D", "E", "F" }, letters1P);
        Assert.Equal(new[] { "A", "AA", "AAA", "B", "C", "D", "E", "F" }, letters2P);
        Assert.All(resource.DjLevel, e => Assert.Equal(HashAlgorithm.Perceptual, e.Algo));

        // PlayMode: SP/DP 各複数エントリ
        Assert.Contains(resource.PlayMode, e => e.Value == "SP");
        Assert.Contains(resource.PlayMode, e => e.Value == "DP");

        // 既存の states (song_select) は維持されている
        Assert.True(resource.States.ContainsKey("song_select"));
        Assert.NotEmpty(resource.States["song_select"]);
    }
}
