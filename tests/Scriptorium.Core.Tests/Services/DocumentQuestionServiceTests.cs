using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentQuestionServiceTests
{
    private static readonly QaOptions Options = new() { MaxQuestionLength = 50, MaxDocumentCharacters = 100 };

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAIProvider> _provider = new(MockBehavior.Strict);
    private readonly DocumentQuestionService _service;

    public DocumentQuestionServiceTests()
    {
        _service = new DocumentQuestionService(_repository.Object, _provider.Object, new QuestionPromptBuilder(Options), Options);
    }

    [Fact]
    public async Task AskAsync_ReadyDocument_ReturnsTheModelAnswer()
    {
        ReadyDocument("Maria Rossi signed the agreement.");
        AnswerWith("Maria Rossi.");

        var result = await _service.AskAsync(_id, "Who signed?", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Answer.Should().Be("Maria Rossi.");
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task AskAsync_SendsTheDocumentTextAndTheTrimmedQuestionToTheModel()
    {
        ReadyDocument("Maria Rossi signed the agreement.");
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("ok"));

        await _service.AskAsync(_id, "   Who signed?  ", CancellationToken.None);

        sent!.Messages[0].Content.Should().Contain("Maria Rossi signed the agreement.");
        sent.Messages[^1].Should().Be(new AiMessage(AiRole.User, "Who signed?"));
    }

    [Fact]
    public async Task AskAsync_PassesTheCancellationTokenToTheModel()
    {
        ReadyDocument("text");
        using var cancellation = new CancellationTokenSource();
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token)).ReturnsAsync(AiResult.Success("ok"));

        await _service.AskAsync(_id, "q", cancellation.Token);

        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task AskAsync_EmptyQuestion_IsRejectedBeforeAnythingIsLoaded(string question)
    {
        var result = await _service.AskAsync(_id, question, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.InvalidQuestion);
        result.Error.Should().Be("The question is empty.");
    }

    [Fact]
    public async Task AskAsync_QuestionOverTheLimit_IsRejectedWithTheLimit()
    {
        var result = await _service.AskAsync(_id, new string('q', 51), CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.InvalidQuestion);
        result.Error.Should().Contain("50 characters");
    }

    [Fact]
    public async Task AskAsync_QuestionExactlyAtTheLimit_IsAccepted()
    {
        ReadyDocument("text");
        AnswerWith("ok");

        var result = await _service.AskAsync(_id, new string('q', 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AskAsync_UnknownDocument_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.AskAsync(_id, "q", CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
    }

    [Theory]
    [InlineData(DocumentStatus.Processing, "processing")]
    [InlineData(DocumentStatus.Failed, "failed")]
    public async Task AskAsync_DocumentNotReady_ReturnsNotReadyNamingTheStatus(DocumentStatus status, string name)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = status });

        var result = await _service.AskAsync(_id, "q", CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        result.Error.Should().Be($"The document is not ready. Current status: {name}.");
    }

    [Fact]
    public async Task AskAsync_ReadyDocumentWithoutText_ReturnsInternal()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((ExtractedText?)null);

        var result = await _service.AskAsync(_id, "q", CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable)]
    [InlineData(AiFailureKind.Timeout)]
    [InlineData(AiFailureKind.Failed)]
    public async Task AskAsync_ModelFailure_IsReportedAsAnInternalErrorWithoutDetails(AiFailureKind kind)
    {
        ReadyDocument("text");
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, "provider detail"));

        var result = await _service.AskAsync(_id, "q", CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().NotContain("provider detail");
    }

    [Fact]
    public async Task AskAsync_DocumentLongerThanTheBudget_ReturnsATruncatedAnswer()
    {
        ReadyDocument(new string('x', 150));
        AnswerWith("ok");

        var result = await _service.AskAsync(_id, "q", CancellationToken.None);

        result.Truncated.Should().BeTrue();
    }

    private void ReadyDocument(string text)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }

    private void AnswerWith(string answer) =>
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(answer));
}
