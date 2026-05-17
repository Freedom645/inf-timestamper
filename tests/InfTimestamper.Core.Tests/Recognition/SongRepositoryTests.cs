using InfTimestamper.Core.Recognition;

namespace InfTimestamper.Core.Tests.Recognition;

public class SongRepositoryTests
{
    [Fact]
    public void LoadFromString_EmptyJson_ReturnsEmptyRepository()
    {
        var repo = SongRepository.LoadFromString("[]");
        Assert.Equal(0, repo.Count);
    }

    [Fact]
    public void LoadFromString_ValidEntries_ReturnsRecords()
    {
        var json = """
        [
          {
            "id": "1",
            "title": "Test Song",
            "title_normalized": "TESTSONG",
            "charts": { "SPN": 5, "SPH": 9, "SPA": 11 }
          },
          {
            "id": "2",
            "title": "Another",
            "title_normalized": "ANOTHER",
            "charts": {}
          }
        ]
        """;

        var repo = SongRepository.LoadFromString(json);
        Assert.Equal(2, repo.Count);
        Assert.Equal("Test Song", repo.All[0].Title);
        Assert.Equal("TESTSONG", repo.All[0].TitleNormalized);
        Assert.Equal(11, repo.All[0].Charts["SPA"]);
    }

    [Fact]
    public void LoadFromString_MissingNormalized_RecomputesFromTitle()
    {
        var json = """
        [
          { "id": "1", "title": "GIGA RAID" }
        ]
        """;

        var repo = SongRepository.LoadFromString(json);
        Assert.Equal("GIGARAID", repo.All[0].TitleNormalized);
    }

    [Fact]
    public void LoadFromFile_BundledSongsJson_LoadsAllRecords()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "InfTimestamper.Core", "Resources", "INFINITAS", "songs.json");

        path = Path.GetFullPath(path);
        Assert.True(File.Exists(path), $"songs.json が見つかりません: {path}");

        var repo = SongRepository.LoadFromFile(path);
        Assert.True(repo.Count > 1000, $"INFINITAS 楽曲数は 1000 以上のはず（実際: {repo.Count}）");

        // ランダムなレコードに正規化済みタイトルが入っていること
        var sample = repo.All[0];
        Assert.False(string.IsNullOrEmpty(sample.Id));
        Assert.False(string.IsNullOrEmpty(sample.Title));
        Assert.False(string.IsNullOrEmpty(sample.TitleNormalized));
    }
}
