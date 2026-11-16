using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class TextPreviewResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Success_CarriesTheContentTheTruncationFlagAndTheTotal(bool truncated)
    {
        var result = TextPreviewResult.Success("hello", truncated, 52310);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("hello");
        result.Truncated.Should().Be(truncated);
        result.TotalCharacters.Should().Be(52310);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(TextPreviewFailureKind.NotFound)]
    [InlineData(TextPreviewFailureKind.NotReady)]
    public void Failure_CarriesKindAndMessageAndNoContent(TextPreviewFailureKind kind)
    {
        var result = TextPreviewResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.Content.Should().BeNull();
        result.Truncated.Should().BeFalse();
        result.TotalCharacters.Should().Be(0);
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }
}
