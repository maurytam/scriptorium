using FluentAssertions;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Dtos;

public class DocumentDtosTests
{
    private static readonly Document Failed = new()
    {
        Id = Guid.NewGuid(),
        FileName = "report.pdf",
        FileType = "pdf",
        FileSizeBytes = 99,
        UploadDate = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
        IsPrivate = true,
        Status = DocumentStatus.Failed,
        FailureReason = "Password-protected or encrypted PDF"
    };

    [Fact]
    public void UploadedDocumentDto_From_MapsFieldsWithLowercaseStatus()
    {
        var dto = UploadedDocumentDto.From(Failed);

        dto.Id.Should().Be(Failed.Id);
        dto.FileName.Should().Be("report.pdf");
        dto.FileSizeBytes.Should().Be(99);
        dto.IsPrivate.Should().BeTrue();
        dto.Status.Should().Be("failed");
    }

    [Fact]
    public void DocumentDetailsDto_From_IncludesFailureReason()
    {
        var dto = DocumentDetailsDto.From(Failed);

        dto.Status.Should().Be("failed");
        dto.FailureReason.Should().Be("Password-protected or encrypted PDF");
    }

    [Fact]
    public void DocumentSummaryDto_From_MapsListFieldsIncludingIsPrivate()
    {
        var dto = DocumentSummaryDto.From(Failed);

        dto.Should().Be(new DocumentSummaryDto(
            Failed.Id, "report.pdf", "pdf", Failed.UploadDate, IsPrivate: true, Status: "failed"));
    }

    [Fact]
    public void ExtractedTextDto_From_MapsFields()
    {
        var text = new ExtractedText { DocumentId = Guid.NewGuid(), Content = "abc", ExtractedAt = DateTimeOffset.UtcNow };

        var dto = ExtractedTextDto.From(text);

        dto.Should().BeEquivalentTo(text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtractedTextPreviewDto_From_CopiesTheDocumentAndTheSuccessfulResult(bool truncated)
    {
        var id = Guid.NewGuid();
        var result = TextPreviewResult.Success("abc", truncated, 52310);

        var dto = ExtractedTextPreviewDto.From(id, result);

        dto.Should().Be(new ExtractedTextPreviewDto(id, "abc", truncated, 52310));
    }

    [Fact]
    public void ExtractedTextPreviewDto_From_AFailedResult_IsAProgrammingError()
    {
        var failed = TextPreviewResult.Failure(TextPreviewFailureKind.NotFound, "nope");

        var act = () => ExtractedTextPreviewDto.From(Guid.NewGuid(), failed);

        act.Should().Throw<InvalidOperationException>();
    }
}
