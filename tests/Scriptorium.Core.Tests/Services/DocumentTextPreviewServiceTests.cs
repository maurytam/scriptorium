using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentTextPreviewServiceTests
{
    private static readonly QaOptions Options = new() { MaxDocumentCharacters = 10 };

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly DocumentTextPreviewService _service;

    public DocumentTextPreviewServiceTests()
    {
        _service = new DocumentTextPreviewService(_repository.Object, Options);
    }

    [Fact]
    public async Task GetPreviewAsync_UnknownDocument_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.GetPreviewAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(TextPreviewFailureKind.NotFound);
    }

    [Theory]
    [InlineData(DocumentStatus.Processing, "processing")]
    [InlineData(DocumentStatus.Failed, "failed")]
    public async Task GetPreviewAsync_DocumentNotReady_ReturnsNotReadyNamingTheStatusAndReadsNoText(
        DocumentStatus status, string expectedName)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = status });

        var result = await _service.GetPreviewAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(TextPreviewFailureKind.NotReady);
        result.Error.Should().Be($"Document is not ready. Current status: {expectedName}.");
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPreviewAsync_ReadyTextWithinTheLimit_ReturnsItWhole()
    {
        ReadyDocumentWithText("short");

        var result = await _service.GetPreviewAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("short");
        result.Truncated.Should().BeFalse();
        result.TotalCharacters.Should().Be(5);
    }

    [Fact]
    public async Task GetPreviewAsync_ReadyTextLongerThanTheLimit_ReturnsTheBeginningAndTheWholeLength()
    {
        ReadyDocumentWithText("0123456789ABCDEF");

        var result = await _service.GetPreviewAsync(_id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("0123456789");
        result.Truncated.Should().BeTrue();
        result.TotalCharacters.Should().Be(16);
    }

    [Fact]
    public async Task GetPreviewAsync_ReadyDocumentWithNoStoredText_ReturnsNotReadyWithItsOwnMessage()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((ExtractedText?)null);

        var result = await _service.GetPreviewAsync(_id, CancellationToken.None);

        result.FailureKind.Should().Be(TextPreviewFailureKind.NotReady);
        result.Error.Should().Be("The extracted text of this document is not available.");
    }

    [Fact]
    public async Task GetPreviewAsync_PassesTheCancellationTokenToTheRepository()
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(r => r.GetByIdAsync(_id, cancellation.Token))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, cancellation.Token))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = "text" });

        var result = await _service.GetPreviewAsync(_id, cancellation.Token);

        result.IsSuccess.Should().BeTrue();
    }

    private void ReadyDocumentWithText(string content)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = content });
    }
}
