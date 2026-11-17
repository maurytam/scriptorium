using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class SignatureResultTests
{
    [Fact]
    public void Success_CarriesTheEntriesInOrderAndNoFailure()
    {
        var entries = new[] { new SignatureEntry("Anna Verdi", "2026-03-14 09:00:00"), new SignatureEntry(null, null) };

        var result = SignatureResult.Success(entries);

        result.IsSuccess.Should().BeTrue();
        result.Signatures.Should().Equal(entries);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithNoEntriesIsStillASuccess()
    {
        var result = SignatureResult.Success([]);

        result.IsSuccess.Should().BeTrue();
        result.Signatures.Should().BeEmpty();
    }

    [Fact]
    public void Failure_CarriesAKindAndAMessageAndAnEmptyListNeverNull()
    {
        var result = SignatureResult.Failure(AskFailureKind.NotReady, "not ready");

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        result.Error.Should().Be("not ready");
        result.Signatures.Should().NotBeNull().And.BeEmpty();
    }
}
