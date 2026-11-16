using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class AskResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Success_CarriesTheAnswerAndTheTruncationFlag(bool truncated)
    {
        var result = AskResult.Success("Paris", truncated);

        result.IsSuccess.Should().BeTrue();
        result.Answer.Should().Be("Paris");
        result.Truncated.Should().Be(truncated);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(AskFailureKind.InvalidQuestion)]
    [InlineData(AskFailureKind.NotFound)]
    [InlineData(AskFailureKind.NotReady)]
    [InlineData(AskFailureKind.ModelUnavailable)]
    [InlineData(AskFailureKind.Timeout)]
    [InlineData(AskFailureKind.Internal)]
    public void Failure_CarriesKindAndMessageAndNoAnswer(AskFailureKind kind)
    {
        var result = AskResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.Answer.Should().BeNull();
        result.Truncated.Should().BeFalse();
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }

    [Fact]
    public void Success_WithoutABackend_IsAnsweredLocally()
    {
        AskResult.Success("Paris", false).Backend.Should().Be(ModelBackend.Local);
    }

    [Theory]
    [InlineData(ModelBackend.Local)]
    [InlineData(ModelBackend.Cloud)]
    public void Success_CarriesTheBackendThatAnswered(ModelBackend backend)
    {
        AskResult.Success("Paris", false, backend).Backend.Should().Be(backend);
    }
}
