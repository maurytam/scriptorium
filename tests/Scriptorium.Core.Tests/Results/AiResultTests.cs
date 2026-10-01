using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class AiResultTests
{
    [Fact]
    public void Success_CarriesTheTextAndNoFailure()
    {
        var result = AiResult.Success("Paris");

        result.IsSuccess.Should().BeTrue();
        result.Text.Should().Be("Paris");
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable)]
    [InlineData(AiFailureKind.Timeout)]
    [InlineData(AiFailureKind.Failed)]
    public void Failure_CarriesKindAndMessageAndNoText(AiFailureKind kind)
    {
        var result = AiResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.Text.Should().BeNull();
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }

    [Fact]
    public void Success_WithAnEmptyText_IsStillASuccess()
    {
        AiResult.Success(string.Empty).IsSuccess.Should().BeTrue();
    }
}
