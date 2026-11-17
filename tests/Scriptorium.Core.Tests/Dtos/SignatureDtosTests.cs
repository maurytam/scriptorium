using FluentAssertions;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Dtos;

public class SignatureDtosTests
{
    [Fact]
    public void From_MapsEachEntryInOrder()
    {
        var result = SignatureResult.Success(
            [new SignatureEntry("Anna Verdi", "2026-03-14 09:00:00 +01:00"), new SignatureEntry("Luca Bianchi", "2026-03-15 10:00:00")]);

        var dto = SignaturesResponseDto.From(result);

        dto.Signatures.Should().Equal(
            new SignatureDto("Anna Verdi", "2026-03-14 09:00:00 +01:00"),
            new SignatureDto("Luca Bianchi", "2026-03-15 10:00:00"));
    }

    [Fact]
    public void From_KeepsAMissingSignerAndAMissingDateAsNull()
    {
        var dto = SignaturesResponseDto.From(SignatureResult.Success([new SignatureEntry(null, null)]));

        dto.Signatures.Should().ContainSingle().Which.Should().Be(new SignatureDto(null, null));
    }

    [Fact]
    public void From_NoSignature_IsAnEmptyList()
    {
        SignaturesResponseDto.From(SignatureResult.Success([])).Signatures.Should().BeEmpty();
    }

    [Fact]
    public void From_AFailedResult_IsAnEmptyListNeverNull()
    {
        var dto = SignaturesResponseDto.From(SignatureResult.Failure(AskFailureKind.NotFound, "gone"));

        dto.Signatures.Should().NotBeNull().And.BeEmpty();
    }
}
