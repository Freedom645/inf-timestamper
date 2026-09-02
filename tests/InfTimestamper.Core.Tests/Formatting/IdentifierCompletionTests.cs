using InfTimestamper.Core.Formatting;
using InfTimestamper.Core.Games;
using InfTimestamper.Core.Models;

namespace InfTimestamper.Core.Tests.Formatting;

/// <summary>
/// 設定画面の識別子サジェスト（<c>Behaviors/IdentifierSuggestion</c>）のロジック。
/// Popup とキー操作は WPF 側に残し、ここでは「どこを候補にするか / どう置き換えるか」を検証する。
/// </summary>
public class IdentifierCompletionTests
{
    private static readonly IReadOnlyList<IdentifierChoice> Choices =
        GameCatalog.IdentifierChoices(GameId.Infinitas);

    [Fact]
    public void TryGetToken_RightAfterDollar_ReturnsEmptyPrefix()
    {
        Assert.True(IdentifierCompletion.TryGetToken("$", 1, out var start, out var prefix));
        Assert.Equal(0, start);
        Assert.Equal(string.Empty, prefix);
    }

    [Fact]
    public void TryGetToken_AfterPartialIdentifier_ReturnsThePrefix()
    {
        const string text = "$timestamp $ti";

        Assert.True(IdentifierCompletion.TryGetToken(text, text.Length, out var start, out var prefix));
        Assert.Equal(11, start);
        Assert.Equal("ti", prefix);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("title", 5)]
    [InlineData("$title ", 7)]     // 識別子として成立しない文字（空白）を挟んだら候補は出さない
    [InlineData("a$title", 1)]     // キャレットが `$` より手前
    public void TryGetToken_ReturnsFalseWhenNotOnAToken(string text, int caret)
    {
        Assert.False(IdentifierCompletion.TryGetToken(text, caret, out _, out _));
    }

    [Fact]
    public void TryGetToken_CaretOutOfRange_ReturnsFalse()
    {
        Assert.False(IdentifierCompletion.TryGetToken("$title", 99, out _, out _));
        Assert.False(IdentifierCompletion.TryGetToken("$title", -1, out _, out _));
    }

    [Fact]
    public void Filter_EmptyPrefix_ReturnsEveryCandidate()
    {
        Assert.Equal(Choices.Count, IdentifierCompletion.Filter(Choices, string.Empty).Count);
    }

    [Fact]
    public void Filter_NarrowsByPrefix()
    {
        var filtered = IdentifierCompletion.Filter(Choices, "d");

        Assert.Equal(new[] { "diff_l", "diff_s", "dj_level" }, filtered.Select(c => c.Key));
    }

    [Fact]
    public void Filter_LongerPrefix_NarrowsFurther()
    {
        var filtered = IdentifierCompletion.Filter(Choices, "diff_");

        Assert.Equal(new[] { "diff_l", "diff_s" }, filtered.Select(c => c.Key));
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(IdentifierCompletion.Filter(Choices, "zzz"));
    }

    [Fact]
    public void Apply_ReplacesTheTypedTokenAndMovesTheCaret()
    {
        const string text = "$timestamp $ti";
        IdentifierCompletion.TryGetToken(text, text.Length, out var start, out _);

        var (result, caret) = IdentifierCompletion.Apply(text, start, text.Length, "title");

        Assert.Equal("$timestamp $title", result);
        Assert.Equal(result.Length, caret);
    }

    [Fact]
    public void Apply_KeepsTheTextAfterTheCaret()
    {
        const string text = "$ti [$diff_s]";
        IdentifierCompletion.TryGetToken(text, 3, out var start, out _);

        var (result, caret) = IdentifierCompletion.Apply(text, start, 3, "title");

        Assert.Equal("$title [$diff_s]", result);
        Assert.Equal(6, caret);
    }

    [Fact]
    public void Apply_ClampsOutOfRangePositions()
    {
        var (result, caret) = IdentifierCompletion.Apply("$ti", 0, 99, "title");

        Assert.Equal("$title", result);
        Assert.Equal(6, caret);
    }
}
