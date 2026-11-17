using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentSummaryServiceTests
{
    private static readonly QaOptions Options = new() { MaxDocumentCharacters = 100 };
    private static readonly string EnoughText = new('a', 60);

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAIProvider> _provider = new(MockBehavior.Strict);
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);
    private readonly DocumentSummaryService _service;

    public DocumentSummaryServiceTests()
    {
        _service = ServiceWith(new RoutingOptions());
    }

    [Fact]
    public async Task SummarizeAsync_ReadyDocument_ReturnsTheLocalModelText()
    {
        ReadyDocument(EnoughText);
        SummariseWith("A short summary.");

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Answer.Should().Be("A short summary.");
        result.Backend.Should().Be(ModelBackend.Local);
        result.Truncated.Should().BeFalse();
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SummarizeAsync_SendsTheRequestTheBuilderMakesForThatText()
    {
        ReadyDocument(EnoughText);
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("ok"));

        await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        sent!.Messages.Should().BeEquivalentTo(new SummaryPromptBuilder(Options).Build(EnoughText).Request.Messages);
    }

    [Fact]
    public async Task SummarizeAsync_ALongDocumentIsSummarisedFromItsFirstPartAndSaysSo()
    {
        ReadyDocument(new string('a', 300));
        SummariseWith("ok");

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeTrue();
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "Ollama is not running.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The model took too long.")]
    public async Task SummarizeAsync_ModelFailureYouCanActOn_KeepsTheProvidersText(
        AiFailureKind kind, AskFailureKind expected, string message)
    {
        ReadyDocument(EnoughText);
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be(message);
    }

    [Fact]
    public async Task SummarizeAsync_AnyOtherModelFailure_IsInternalWithNoDetail()
    {
        ReadyDocument(EnoughText);
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail"));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task SummarizeAsync_ABlankModelText_IsInternalNeverAnEmptySummary(string blank)
    {
        ReadyDocument(EnoughText);
        SummariseWith(blank);

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Fact]
    public async Task SummarizeAsync_PassesTheCancellationTokenToTheModel()
    {
        ReadyDocument(EnoughText);
        using var cancellation = new CancellationTokenSource();
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token)).ReturnsAsync(AiResult.Success("ok"));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, cancellation.Token);

        result.IsSuccess.Should().BeTrue();
        _provider.VerifyAll();
    }

    [Fact]
    public async Task SummarizeAsync_UnknownDocument_IsNotFoundAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
        NoModelWasCalled();
    }

    [Fact]
    public async Task SummarizeAsync_DocumentNotReady_IsNotReadyTheTextIsNotReadAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Processing });

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        NoModelWasCalled();
    }

    [Fact]
    public async Task SummarizeAsync_49Characters_IsNotEnoughTextAndNeitherModelNorRouterIsUsed()
    {
        ReadyDocument(new string('a', 49));
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
        result.Error.Should().Be("The document has too little text to summarise.");
        NoModelWasCalled();
    }

    [Fact]
    public async Task SummarizeAsync_50Characters_IsSummarised()
    {
        ReadyDocument(new string('a', 50));
        SummariseWith("ok");

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SummarizeAsync_SpacesAroundTheTextDoNotCount()
    {
        ReadyDocument(new string(' ', 50) + "short text" + new string(' ', 50));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
        NoModelWasCalled();
    }

    // ---- Routing: a summary follows the same rules as a question ----

    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    // 75 % of the limit of 100 in these tests: the one signal a summary can raise (a score of 1).
    private static readonly string LongText = new('a', 80);
    private static readonly string ShortText = new('a', 60);

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task SummarizeAsync_CloudOff_AutoAndLocalAreAnsweredLocally(ModelPreference preference)
    {
        ReadyDocument(EnoughText);
        SummariseWith("local");

        var result = await _service.SummarizeAsync(_id, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SummarizeAsync_CloudOff_CloudIsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(EnoughText);

        var result = await _service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        result.Error.Should().Be(CloudNotAvailable);
        NoModelWasCalled();
    }

    [Fact]
    public async Task SummarizeAsync_PrivateDocumentWithCloud_IsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(EnoughText, isPrivate: true);

        var result = await _service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        result.Error.Should().Be(PrivateRefused);
        NoModelWasCalled();
    }

    [Fact]
    public async Task SummarizeAsync_TheChecksOfTheDocumentComeBeforeRouting_UnknownDocument()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
    }

    [Fact]
    public async Task SummarizeAsync_TheChecksOfTheDocumentComeBeforeRouting_NotReady()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Failed });

        var result = await _service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
    }

    [Fact]
    public async Task SummarizeAsync_TheChecksOfTheDocumentComeBeforeRouting_TooLittleText()
    {
        ReadyDocument(new string('a', 49));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
    }

    [Fact]
    public async Task SummarizeAsync_CloudOn_WithTheDefaultThreshold_ALongDocumentUnderAutoStaysLocal()
    {
        ReadyDocument(LongText);
        SummariseWith("local");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true); // threshold 2, score at most 1

        var result = await service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SummarizeAsync_CloudOn_WithThresholdZero_ALongDocumentUnderAutoGoesToTheCloud()
    {
        ReadyDocument(LongText);
        AnswerFromCloudWith("cloud");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
        result.Answer.Should().Be("cloud");
    }

    [Fact]
    public async Task SummarizeAsync_CloudOn_WithThresholdZero_AShortDocumentUnderAutoStaysLocal()
    {
        ReadyDocument(ShortText);
        SummariseWith("local");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
    }

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task SummarizeAsync_CloudOn_APrivateDocumentNeverReachesTheCloud(ModelPreference preference)
    {
        ReadyDocument(LongText, isPrivate: true);
        SummariseWith("local");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SummarizeAsync_CloudOn_CloudOnANonPrivateDocumentReachesTheCloud()
    {
        ReadyDocument(ShortText);
        AnswerFromCloudWith("cloud");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "The cloud model is not available. Check the cloud settings and your connection.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The cloud model did not answer in time. Please try again.")]
    public async Task SummarizeAsync_CloudFails_KeepsItsTextAndNeverFallsBackToTheLocalModel(
        AiFailureKind kind, AskFailureKind expected, string message)
    {
        ReadyDocument(ShortText);
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

        var result = await service.SummarizeAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be(message);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<bool, ModelPreference, ModelRouterTests.CloudState, bool> WholeTable()
    {
        var data = new TheoryData<bool, ModelPreference, ModelRouterTests.CloudState, bool>();
        foreach (var isPrivate in new[] { false, true })
        foreach (var preference in Enum.GetValues<ModelPreference>())
        foreach (var state in Enum.GetValues<ModelRouterTests.CloudState>())
        foreach (var longText in new[] { false, true })
        {
            data.Add(isPrivate, preference, state, longText);
        }

        return data;
    }

    // SC-003: private yes/no x preference x cloud state x score, against an oracle written apart from the router.
    // The threshold is 0, so the score of a summary (0 for a short text, 1 for a long one) is what Auto looks at.
    [Theory]
    [MemberData(nameof(WholeTable))]
    public async Task SummarizeAsync_WholeTable_GivesWhatTheRouterGivesForAQuestion(
        bool isPrivate, ModelPreference preference, ModelRouterTests.CloudState state, bool longText)
    {
        ReadyDocument(longText ? LongText : ShortText, isPrivate);
        SummariseWith("local");
        AnswerFromCloudWith("cloud");
        var enabled = state != ModelRouterTests.CloudState.Off;
        var configured = state != ModelRouterTests.CloudState.OnNotConfigured;
        var service = ServiceWith(new RoutingOptions { CloudEnabled = enabled, ComplexityThreshold = 0 }, configured);

        var result = await service.SummarizeAsync(_id, preference, CancellationToken.None);

        var usable = enabled && configured;
        if (isPrivate && preference == ModelPreference.Cloud)
        {
            result.FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        }
        else if (!isPrivate && preference == ModelPreference.Cloud && !usable)
        {
            result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        }
        else
        {
            var toCloud = !isPrivate && usable && (preference == ModelPreference.Cloud || (preference == ModelPreference.Auto && longText));
            result.IsSuccess.Should().BeTrue();
            result.Backend.Should().Be(toCloud ? ModelBackend.Cloud : ModelBackend.Local);
        }
    }

    // ---- A long document: only the first part is read, and the result says so ----

    [Fact]
    public async Task SummarizeAsync_ATextLongerThanTheLimit_IsTruncatedAndTheModelNeverSeesTheEnd()
    {
        ReadyDocument(new string('a', 100) + "THE-END-MARK");
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("ok"));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Truncated.Should().BeTrue();
        sent!.Messages.Should().NotContain(m => m.Content.Contains("THE-END-MARK"));
        sent.Messages[0].Content.Should().Contain(new string('a', 100));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(60)]
    public async Task SummarizeAsync_ATextAtOrUnderTheLimit_IsNotTruncated(int length)
    {
        ReadyDocument(new string('a', length));
        SummariseWith("ok");

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task SummarizeAsync_TheCutNeverEndsOnHalfOfACharacter()
    {
        ReadyDocument(new string('a', 99) + "\U0001F600" + "tail-of-the-text"); // a surrogate pair across the limit of 100
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("ok"));

        var result = await _service.SummarizeAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeTrue();
        var content = sent!.Messages[0].Content;
        content.Should().NotContain("\U0001F600");
        content.Should().NotContain("tail-of-the-text");
        content.Any(char.IsLowSurrogate).Should().BeFalse();
    }

    private void AnswerFromCloudWith(string text) =>
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(text));

    private void NoModelWasCalled()
    {
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private DocumentSummaryService ServiceWith(RoutingOptions routing, bool cloudConfigured = false)
    {
        _cloud.SetupGet(c => c.IsConfigured).Returns(cloudConfigured);
        var router = new ModelRouter(_provider.Object, _cloud.Object, routing);
        return new DocumentSummaryService(_repository.Object, router, new ComplexityScorer(Options), new SummaryPromptBuilder(Options));
    }

    private void SummariseWith(string text) =>
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(text));

    private void ReadyDocument(string text, bool isPrivate = false)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, IsPrivate = isPrivate });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }
}
