using FluentAssertions;
using Moq;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentSignatureServiceTests
{
    private const string Wording = "file firmato con firma digitale";

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);

    private DocumentSignatureService Service() => new(_repository.Object);

    private void ReadyDocument(string fileType, string text)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, FileType = fileType });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }

    [Fact]
    public async Task ReadAsync_UnknownDocument_IsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.NotFound);
        result.Error.Should().Be("Document not found.");
    }

    [Fact]
    public async Task ReadAsync_DocumentNotReady_IsNotReadyAndTheTextIsNotRead()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Processing, FileType = "pdf" });

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        result.Error.Should().Be("The document is not ready. Current status: processing.");
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadAsync_ReadyDocumentWithNoStoredText_IsInternal()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, FileType = "pdf" });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((ExtractedText?)null);

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The text of the document is not available.");
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("xlsx")]
    public async Task ReadAsync_OneLineAtTheStart_GivesOneEntry(string fileType)
    {
        ReadyDocument(fileType, $"{Wording} da Mario Rossi — 2026-03-14 10:32:00 +01:00\n\nText.");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Signatures.Should().Equal(new SignatureEntry("Mario Rossi", "2026-03-14 10:32:00 +01:00"));
    }

    [Fact]
    public async Task ReadAsync_SeveralLines_GiveTheEntriesInTheOrderOfTheFile()
    {
        ReadyDocument("xlsx", $"{Wording} da Anna Verdi — 2026-03-14 09:00:00\n{Wording} (firmatario sconosciuto)\n\nText.");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.Signatures.Should().Equal(new SignatureEntry("Anna Verdi", "2026-03-14 09:00:00"), new SignatureEntry(null, null));
    }

    [Theory]
    [InlineData("PDF")]
    [InlineData("Docx")]
    [InlineData("XLSX")]
    public async Task ReadAsync_FileTypeInAnyLetterCase_IsRecognised(string fileType)
    {
        ReadyDocument(fileType, $"{Wording} da Mario Rossi\n\nText.");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.Signatures.Should().ContainSingle();
    }

    [Fact]
    public async Task ReadAsync_TextFileWhoseTextStartsWithNoteLines_HasNoSignature()
    {
        ReadyDocument("txt", $"{Wording} da Mario Rossi — 2026-03-14 10:32:00\n\nA text file only quoting the wording.");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Signatures.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WordingOnlyFurtherDown_HasNoSignature()
    {
        ReadyDocument("pdf", $"Page text.\n\n{Wording} da Mario Rossi");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.Signatures.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_UnsignedDocument_HasNoSignature()
    {
        ReadyDocument("docx", "Just a document.");

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Signatures.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_VeryLongText_StillGivesTheLinesAtItsStart()
    {
        ReadyDocument("pdf", $"{Wording} da Mario Rossi\n\n" + new string('x', 50_000));

        var result = await Service().ReadAsync(_id, CancellationToken.None);

        result.Signatures.Should().Equal(new SignatureEntry("Mario Rossi", null));
    }

    [Fact]
    public void Constructor_NeedsOnlyTheRepository()
    {
        typeof(DocumentSignatureService).GetConstructors().Should().ContainSingle()
            .Which.GetParameters().Select(p => p.ParameterType).Should().Equal(typeof(IDocumentRepository));
    }
}
