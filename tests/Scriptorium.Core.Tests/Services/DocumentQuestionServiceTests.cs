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
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);
    private readonly DocumentQuestionService _service;

    public DocumentQuestionServiceTests()
    {
        _service = ServiceWith(new RoutingOptions());
    }

    [Fact]
    public async Task AskAsync_ReadyDocument_ReturnsTheModelAnswer()
    {
        ReadyDocument("Maria Rossi signed the agreement.");
        AnswerWith("Maria Rossi.");

        var result = await _service.AskAsync(_id, "Who signed?", null, ModelPreference.Auto, CancellationToken.None);

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

        await _service.AskAsync(_id, "   Who signed?  ", null, ModelPreference.Auto, CancellationToken.None);

        sent!.Messages[0].Content.Should().Contain("Maria Rossi signed the agreement.");
        sent.Messages[^1].Should().Be(new AiMessage(AiRole.User, "Who signed?"));
    }

    [Fact]
    public async Task AskAsync_PassesTheCancellationTokenToTheModel()
    {
        ReadyDocument("text");
        using var cancellation = new CancellationTokenSource();
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token)).ReturnsAsync(AiResult.Success("ok"));

        await _service.AskAsync(_id, "q", null, ModelPreference.Auto, cancellation.Token);

        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task AskAsync_EmptyQuestion_IsRejectedBeforeAnythingIsLoaded(string question)
    {
        var result = await _service.AskAsync(_id, question, null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.InvalidQuestion);
        result.Error.Should().Be("The question is empty.");
    }

    [Fact]
    public async Task AskAsync_QuestionOverTheLimit_IsRejectedWithTheLimit()
    {
        var result = await _service.AskAsync(_id, new string('q', 51), null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.InvalidQuestion);
        result.Error.Should().Contain("50 characters");
    }

    [Fact]
    public async Task AskAsync_QuestionExactlyAtTheLimit_IsAccepted()
    {
        ReadyDocument("text");
        AnswerWith("ok");

        var result = await _service.AskAsync(_id, new string('q', 50), null, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AskAsync_UnknownDocument_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
    }

    [Theory]
    [InlineData(DocumentStatus.Processing, "processing")]
    [InlineData(DocumentStatus.Failed, "failed")]
    public async Task AskAsync_DocumentNotReady_ReturnsNotReadyNamingTheStatus(DocumentStatus status, string name)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = status });

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        result.Error.Should().Be($"The document is not ready. Current status: {name}.");
    }

    [Fact]
    public async Task AskAsync_ReadyDocumentWithoutText_ReturnsInternal()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((ExtractedText?)null);

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable)]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout)]
    public async Task AskAsync_ModelFailureTheUserCanActOn_KeepsItsKindAndTheProviderMessage(AiFailureKind kind, AskFailureKind expected)
    {
        ReadyDocument("text");
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, "message for the user"));

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be("message for the user");
    }

    [Fact]
    public async Task AskAsync_AnyOtherModelFailure_IsReportedAsAnInternalErrorWithoutDetails()
    {
        ReadyDocument("text");
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "provider detail"));

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().NotContain("provider detail");
    }

    [Fact]
    public async Task AskAsync_DocumentLongerThanTheBudget_ReturnsATruncatedAnswer()
    {
        ReadyDocument(new string('x', 150));
        AnswerWith("ok");

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task AskAsync_WithHistory_SendsTheEarlierExchangesToTheModelBeforeTheQuestion()
    {
        ReadyDocument("Maria Rossi signed the agreement in May.");
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("ok"));
        var history = new[] { new ChatExchange("Who signed?", "Maria Rossi.") };

        await _service.AskAsync(_id, "When?", history, ModelPreference.Auto, CancellationToken.None);

        sent!.Messages.Skip(1).Should().Equal(
            new AiMessage(AiRole.User, "Who signed?"),
            new AiMessage(AiRole.Assistant, "Maria Rossi."),
            new AiMessage(AiRole.User, "When?"));
    }

    // ---- routing (feature 005, user story 1) ----

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task AskAsync_PrivateDocument_IsAnsweredLocallyAndNeverReachesTheCloud(ModelPreference preference)
    {
        ReadyDocument("Confidential text.", isPrivate: true);
        AnswerWith("ok");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.AskAsync(_id, "Compare and explain why?", null, preference, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_WithTheDeliveredSettings_AnswersLocallyAndSaysSo()
    {
        ReadyDocument("Maria Rossi signed.");
        AnswerWith("Maria Rossi.");

        var result = await _service.AskAsync(_id, "Who signed?", null, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
    }

    [Fact]
    public async Task AskAsync_CloudChosenWhileItIsOff_FailsWithTheExplicitMessageAndCallsNoModel()
    {
        ReadyDocument("text");

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        result.Error.Should().Be(
            "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_CloudChosenForAPrivateDocument_IsRefusedAndCallsNoModel()
    {
        ReadyDocument("text", isPrivate: true);

        var result = await _service.AskAsync(_id, "q", null, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        result.Error.Should().Be("This document is private, so it can only be answered on this computer. Choose Auto or Local.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_RoutingComesAfterTheChecksOnTheQuestionAndTheDocument()
    {
        var invalid = await _service.AskAsync(_id, "  ", null, ModelPreference.Cloud, CancellationToken.None);
        invalid.FailureKind.Should().Be(AskFailureKind.InvalidQuestion);

        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);
        var missing = await _service.AskAsync(_id, "q", null, ModelPreference.Cloud, CancellationToken.None);
        missing.FailureKind.Should().Be(AskFailureKind.NotFound);

        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Processing, IsPrivate = true });
        var notReady = await _service.AskAsync(_id, "q", null, ModelPreference.Cloud, CancellationToken.None);
        notReady.FailureKind.Should().Be(AskFailureKind.NotReady);
    }

    // The score is built from the trimmed question, the length of the text and the number of earlier exchanges.
    // A cloud that is on with a threshold of 0 makes any single signal visible: it sends the question to the cloud.
    [Fact]
    public async Task AskAsync_TheScoreUsesTheTrimmedQuestion()
    {
        ReadyDocument("short");
        AnswerWith("local");
        var padded = "   " + new string('a', 399) + "   ";
        var service = ServiceWith(
            new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true,
            qa: Options with { MaxQuestionLength = 1000 });

        var result = await service.AskAsync(_id, padded, null, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local); // 399 characters once trimmed: no long-question signal
    }

    [Fact]
    public async Task AskAsync_TheScoreUsesTheLengthOfTheTextTheModelReads()
    {
        ReadyDocument(new string('x', 75)); // 75 % of the limit of 100 in these tests
        AnswerFromCloudWith("cloud");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.AskAsync(_id, "q", null, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
    }

    [Fact]
    public async Task AskAsync_TheScoreCountsTheEarlierExchanges()
    {
        ReadyDocument("short");
        AnswerFromCloudWith("cloud");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);
        var history = new[] { new ChatExchange("a", "b"), new ChatExchange("c", "d"), new ChatExchange("e", "f") };

        var result = await service.AskAsync(_id, "q", history, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
    }

    // ---- routing (feature 005, user story 3): the cloud switched on, proved with a stand-in that has a key ----

    private const string ComplexQuestion = "Compare the two clauses? Explain why they differ?"; // two marks + reasoning words
    private static readonly string LongText = new('x', 80); // 80 % of the limit of 100 in these tests: a long reading

    private DocumentQuestionService CloudOn() => ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

    [Fact]
    public async Task AskAsync_CloudOn_AComplexQuestionUnderAutoIsAnsweredByTheStandIn()
    {
        ReadyDocument(LongText);
        AnswerFromCloudWith("from the cloud");

        var result = await CloudOn().AskAsync(_id, ComplexQuestion, null, ModelPreference.Auto, CancellationToken.None);

        result.Answer.Should().Be("from the cloud");
        result.Backend.Should().Be(ModelBackend.Cloud);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_CloudOn_AQuestionAtTheThresholdStaysLocal()
    {
        ReadyDocument("short"); // score 2: two marks and reasoning words, no long reading
        AnswerWith("local");

        var result = await CloudOn().AskAsync(_id, ComplexQuestion, null, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task AskAsync_CloudOn_APrivateDocumentNeverReachesTheStandIn(ModelPreference preference)
    {
        ReadyDocument(LongText, isPrivate: true);
        AnswerWith("local");

        var result = await CloudOn().AskAsync(_id, ComplexQuestion, null, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_CloudOn_PreferenceLocalNeverReachesTheStandIn()
    {
        ReadyDocument(LongText);
        AnswerWith("local");

        var result = await CloudOn().AskAsync(_id, ComplexQuestion, null, ModelPreference.Local, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("q", "short")]
    [InlineData(ComplexQuestion, "a long enough text to count as a long reading, but it does not matter for this choice at all")]
    public async Task AskAsync_CloudOn_PreferenceCloudOnANonPrivateDocumentReachesTheStandInWhateverTheComplexity(string question, string text)
    {
        ReadyDocument(text);
        AnswerFromCloudWith("from the cloud");

        var result = await CloudOn().AskAsync(_id, question, null, ModelPreference.Cloud, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task AskAsync_TheSameSetUpWithTheCloudOff_SendsEverythingToTheLocalProvider(ModelPreference preference)
    {
        ReadyDocument(LongText);
        AnswerWith("local");
        var cloudOff = ServiceWith(new RoutingOptions { CloudEnabled = false }, cloudConfigured: true);

        var result = await cloudOff.AskAsync(_id, ComplexQuestion, null, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_TheSameSetUpWithTheCloudOff_RefusesCloudWithoutCallingAnyone()
    {
        ReadyDocument(LongText);
        var cloudOff = ServiceWith(new RoutingOptions { CloudEnabled = false }, cloudConfigured: true);

        var result = await cloudOff.AskAsync(_id, ComplexQuestion, null, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "The cloud model is not available. Check the cloud settings and your connection.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The cloud model did not answer in time. Please try again.")]
    public async Task AskAsync_CloudOn_WhenTheStandInFails_ThereIsNoFallbackToTheLocalProvider(
        AiFailureKind aiKind, AskFailureKind expected, string message)
    {
        ReadyDocument(LongText);
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(aiKind, message));

        var result = await CloudOn().AskAsync(_id, ComplexQuestion, null, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be(message);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private DocumentQuestionService ServiceWith(RoutingOptions routing, bool cloudConfigured = false, QaOptions? qa = null)
    {
        qa ??= Options;
        _cloud.SetupGet(c => c.IsConfigured).Returns(cloudConfigured);
        var router = new ModelRouter(_provider.Object, _cloud.Object, routing);
        return new DocumentQuestionService(
            _repository.Object, router, new ComplexityScorer(qa), new QuestionPromptBuilder(qa), qa);
    }

    private void AnswerFromCloudWith(string answer) =>
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(answer));

    private void ReadyDocument(string text, bool isPrivate = false)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, IsPrivate = isPrivate });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }

    private void AnswerWith(string answer) =>
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(answer));
}
