using System.Text;
using InfTimestamper.Core.YouTube;

namespace InfTimestamper.Core.Tests.YouTube;

public class YouTubeDescriptionComposerTests
{
    private const string Heading = "▼タイムスタンプ";

    [Fact]
    public void EmptyDescription_WritesOnlyBlock()
    {
        var result = YouTubeDescriptionComposer.Compose("", Heading, new[] { "00:00:00 配信開始", "00:05:00 曲A" }, out var dropped);

        Assert.Equal("▼タイムスタンプ\n00:00:00 配信開始\n00:05:00 曲A", result);
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void DescriptionWithoutHeading_AppendsBlockAfterBlankLine()
    {
        var result = YouTubeDescriptionComposer.Compose("今日は皆伝を目指します\n\n", Heading, new[] { "00:05:00 曲A" }, out _);

        Assert.Equal("今日は皆伝を目指します\n\n▼タイムスタンプ\n00:05:00 曲A", result);
    }

    [Fact]
    public void DescriptionWithHeading_ReplacesFromHeadingToEnd()
    {
        var existing = "説明文\n\n▼タイムスタンプ\n00:05:00 曲A";

        var result = YouTubeDescriptionComposer.Compose(existing, Heading, new[] { "00:05:00 曲A", "00:09:00 曲B" }, out _);

        Assert.Equal("説明文\n\n▼タイムスタンプ\n00:05:00 曲A\n00:09:00 曲B", result);
    }

    [Fact]
    public void Heading_UsesLastOccurrence()
    {
        // 手書きの説明文中に同じ見出しがあっても、末尾側のブロックだけを置き換える
        var existing = "▼タイムスタンプ\nは下にあります\n\n▼タイムスタンプ\n00:01:00 古い";

        var result = YouTubeDescriptionComposer.Compose(existing, Heading, new[] { "00:02:00 新しい" }, out _);

        Assert.Equal("▼タイムスタンプ\nは下にあります\n\n▼タイムスタンプ\n00:02:00 新しい", result);
    }

    [Fact]
    public void CrLf_IsNormalized()
    {
        var result = YouTubeDescriptionComposer.Compose("説明文\r\n\r\n▼タイムスタンプ\r\n00:01:00 古い", Heading, new[] { "00:02:00 新しい" }, out _);

        Assert.Equal("説明文\n\n▼タイムスタンプ\n00:02:00 新しい", result);
    }

    [Fact]
    public void AngleBrackets_AreReplacedWithFullWidth()
    {
        // 概要欄に < > を含めると API が invalidDescription で弾く
        var result = YouTubeDescriptionComposer.Compose("", Heading, new[] { "00:05:00 <<SONG>>" }, out _);

        Assert.EndsWith("00:05:00 ＜＜SONG＞＞", result);
    }

    [Fact]
    public void OverLimit_DropsTrailingLines()
    {
        var lines = Enumerable.Range(0, 400).Select(i => $"01:{i / 60:00}:{i % 60:00} とても長い曲名のテスト {i}").ToList();

        var result = YouTubeDescriptionComposer.Compose("説明文", Heading, lines, out var dropped);

        Assert.True(Encoding.UTF8.GetByteCount(result) <= YouTubeDescriptionComposer.MaxDescriptionBytes);
        Assert.True(dropped > 0);
        Assert.Contains(lines[0], result);
        Assert.DoesNotContain(lines[^1], result);
    }
}
