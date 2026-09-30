using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class DeleteResultTests
{
    [Fact]
    public void Success_HasNoFailure()
    {
        var result = DeleteResult.Success();

        result.IsSuccess.Should().BeTrue();
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(DeleteFailureKind.NotFound)]
    [InlineData(DeleteFailureKind.StillProcessing)]
    [InlineData(DeleteFailureKind.Internal)]
    public void Failure_CarriesKindAndError(DeleteFailureKind kind)
    {
        var result = DeleteResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }
}
