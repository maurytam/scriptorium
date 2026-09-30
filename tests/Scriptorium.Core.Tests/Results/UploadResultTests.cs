using FluentAssertions;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class UploadResultTests
{
    [Fact]
    public void Success_CarriesDocumentAndNoFailure()
    {
        var document = new Document { Id = Guid.NewGuid() };

        var result = UploadResult.Success(document);

        result.IsSuccess.Should().BeTrue();
        result.Document.Should().BeSameAs(document);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(UploadFailureKind.UnsupportedType)]
    [InlineData(UploadFailureKind.FileTooLarge)]
    [InlineData(UploadFailureKind.Internal)]
    public void Failure_CarriesKindAndError(UploadFailureKind kind)
    {
        var result = UploadResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.Document.Should().BeNull();
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }
}
