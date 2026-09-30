using FluentAssertions;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var result = Result.Failure("boom");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("boom");
    }

    [Fact]
    public void GenericSuccess_CarriesValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GenericFailure_CarriesErrorAndNoValue()
    {
        var result = Result<string>.Failure("nope");

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be("nope");
    }
}
