using FluentAssertions;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class TextExcerptTests
{
    [Fact]
    public void Of_TextShorterThanTheLimit_IsReturnedUnchanged()
    {
        var excerpt = TextExcerpt.Of("hello", 10);

        excerpt.Text.Should().Be("hello");
        excerpt.Truncated.Should().BeFalse();
        excerpt.OriginalLength.Should().Be(5);
    }

    [Fact]
    public void Of_TextExactlyAtTheLimit_IsNotTruncated()
    {
        var excerpt = TextExcerpt.Of("0123456789", 10);

        excerpt.Text.Should().Be("0123456789");
        excerpt.Truncated.Should().BeFalse();
        excerpt.OriginalLength.Should().Be(10);
    }

    [Fact]
    public void Of_TextLongerThanTheLimit_KeepsExactlyTheFirstCharacters()
    {
        var excerpt = TextExcerpt.Of("0123456789ABC", 10);

        excerpt.Text.Should().Be("0123456789");
        excerpt.Truncated.Should().BeTrue();
        excerpt.OriginalLength.Should().Be(13);
    }

    [Fact]
    public void Of_CutFallingBetweenTheHalvesOfASurrogatePair_MovesOneUnitBack()
    {
        var text = "01234567" + "😀" + "XYZ"; // the emoji takes two units: indexes 8 and 9

        var excerpt = TextExcerpt.Of(text, 9);

        excerpt.Text.Should().Be("01234567");
        excerpt.Truncated.Should().BeTrue();
        excerpt.OriginalLength.Should().Be(text.Length);
    }

    [Fact]
    public void Of_CutRightAfterASurrogatePair_KeepsTheWholeCharacter()
    {
        var text = "01234567" + "😀" + "XYZ";

        var excerpt = TextExcerpt.Of(text, 10);

        excerpt.Text.Should().Be("01234567😀");
    }

    [Fact]
    public void Of_LimitOfZero_GivesAnEmptyTruncatedText()
    {
        var excerpt = TextExcerpt.Of("hello", 0);

        excerpt.Text.Should().BeEmpty();
        excerpt.Truncated.Should().BeTrue();
        excerpt.OriginalLength.Should().Be(5);
    }

    [Fact]
    public void Of_EmptyText_IsNotTruncated()
    {
        var excerpt = TextExcerpt.Of(string.Empty, 10);

        excerpt.Text.Should().BeEmpty();
        excerpt.Truncated.Should().BeFalse();
        excerpt.OriginalLength.Should().Be(0);
    }
}
