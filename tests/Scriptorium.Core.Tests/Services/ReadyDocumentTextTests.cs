using FluentAssertions;
using Moq;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class ReadyDocumentTextTests
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);

    [Fact]
    public async Task LoadAsync_UnknownDocument_IsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.NotFound);
        result.Error.Should().Be("Document not found.");
    }

    [Theory]
    [InlineData(DocumentStatus.Processing, "processing")]
    [InlineData(DocumentStatus.Failed, "failed")]
    public async Task LoadAsync_DocumentNotReady_IsNotReadyAndTheTextIsNotRead(DocumentStatus status, string word)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = status });

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        result.Error.Should().Be($"The document is not ready. Current status: {word}.");
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_ReadyDocumentWithoutExtractedText_IsInternal()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((ExtractedText?)null);

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The text of the document is not available.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_ReadyDocument_GivesTheTextAndWhetherItIsPrivate(bool isPrivate)
    {
        ReadyDocument("Maria Rossi signed.", isPrivate);

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Maria Rossi signed.");
        result.IsPrivate.Should().Be(isPrivate);
    }

    [Fact]
    public async Task LoadAsync_PassesTheCancellationTokenToTheRepository()
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(r => r.GetByIdAsync(_id, cancellation.Token))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, cancellation.Token))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = "text" });

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, cancellation.Token);

        result.IsSuccess.Should().BeTrue();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task LoadAsync_ReadyDocument_ExposesItsFileType()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, FileType = "docx" });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = "text" });

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.FileType.Should().Be("docx");
    }

    [Fact]
    public async Task LoadAsync_FailedLoad_HasNoFileType()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await ReadyDocumentText.LoadAsync(_repository.Object, _id, CancellationToken.None);

        result.FileType.Should().BeEmpty();
    }

    private void ReadyDocument(string text, bool isPrivate)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, IsPrivate = isPrivate });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }
}
