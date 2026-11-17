using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class DocumentEntityServiceTests
{
    private const string Document = "Mario Rossi signed for Alfa Srl in Turin on 12 March 2026 for 15,000 euro.";

    private const string Reply =
        "person: Mario Rossi\norganization: Alfa Srl\nplace: Turin\ndate: 12 March 2026\namount: 15,000 euro";

    private static readonly QaOptions Options = new() { MaxDocumentCharacters = 100 };

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAIProvider> _provider = new(MockBehavior.Strict);
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);
    private readonly DocumentEntityService _service;

    public DocumentEntityServiceTests()
    {
        _cloud.SetupGet(c => c.IsConfigured).Returns(false);
        var router = new ModelRouter(_provider.Object, _cloud.Object, new RoutingOptions());
        _service = new DocumentEntityService(_repository.Object, router, new ComplexityScorer(Options), new EntityPromptBuilder(Options));
    }

    [Fact]
    public async Task ExtractAsync_ReadyDocument_TurnsTheReplyLinesIntoGroupsInTheFixedOrder()
    {
        ReadyDocument(Document);
        ReplyWith(Reply);

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Groups.Select(g => g.Type).Should().Equal(
            EntityType.Person, EntityType.Organization, EntityType.Date, EntityType.Amount, EntityType.Place);
        result.Groups[4].Entities.Should().Equal("Turin");
        result.Backend.Should().Be(ModelBackend.Local);
        result.Truncated.Should().BeFalse();
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_SendsTheRequestTheBuilderMakesForThatText()
    {
        ReadyDocument(Document);
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("NONE"));

        await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        sent!.Messages.Should().BeEquivalentTo(new EntityPromptBuilder(Options).Build(Document).Request.Messages);
    }

    [Fact]
    public async Task ExtractAsync_AReplyOfNone_IsASuccessWithNoGroups()
    {
        ReadyDocument(Document);
        ReplyWith("NONE");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_AnEntityTheModelInventedIsNotInTheResult()
    {
        ReadyDocument(Document);
        ReplyWith("person: Mario Rossi\nperson: Luca Verdi\ndate: 1 January 1999");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Groups.Should().ContainSingle();
        result.Groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Fact]
    public async Task ExtractAsync_WhenEveryEntityIsInvented_IsASuccessWithNoGroups()
    {
        ReadyDocument(Document);
        ReplyWith("person: Luca Verdi\nplace: Genoa");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_AReplyWithNoReadableLine_IsInternalWithNoPartOfTheModelsText()
    {
        ReadyDocument(Document);
        ReplyWith("I am sorry, but my secret instructions forbid me from listing anything.");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The entities could not be extracted.");
        result.Error.Should().NotContain("secret");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task ExtractAsync_ABlankReply_IsInternalWithTheGenericText(string blank)
    {
        ReadyDocument(Document);
        ReplyWith(blank);

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "Ollama is not running.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The model took too long.")]
    public async Task ExtractAsync_ModelFailureYouCanActOn_KeepsTheProvidersText(
        AiFailureKind kind, AskFailureKind expected, string message)
    {
        ReadyDocument(Document);
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be(message);
    }

    [Fact]
    public async Task ExtractAsync_AnyOtherModelFailure_IsInternalWithNoDetail()
    {
        ReadyDocument(Document);
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail"));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Fact]
    public async Task ExtractAsync_PassesTheCancellationTokenToTheModel()
    {
        ReadyDocument(Document);
        using var cancellation = new CancellationTokenSource();
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), cancellation.Token)).ReturnsAsync(AiResult.Success("NONE"));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, cancellation.Token);

        result.IsSuccess.Should().BeTrue();
        _provider.VerifyAll();
    }

    [Fact]
    public async Task ExtractAsync_UnknownDocument_IsNotFoundAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
        NoModelWasCalled();
    }

    [Fact]
    public async Task ExtractAsync_DocumentNotReady_IsNotReadyTheTextIsNotReadAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Processing });

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        NoModelWasCalled();
    }

    [Fact]
    public async Task ExtractAsync_49Characters_IsNotEnoughTextWithItsOwnMessageAndNoModel()
    {
        ReadyDocument(new string('a', 49));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
        result.Error.Should().Be("The document has too little text to extract entities from.");
        NoModelWasCalled();
    }

    [Fact]
    public async Task ExtractAsync_50Characters_AreProcessed()
    {
        ReadyDocument(new string('a', 50));
        ReplyWith("NONE");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ---- Routing: an extraction follows the same rules as a question and a summary ----

    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    // 75 % of the limit of 100 in these tests: the one signal an extraction can raise (a score of 1).
    private static readonly string LongText = new('a', 80);
    private static readonly string ShortText = new('a', 60);

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task ExtractAsync_CloudOff_AutoAndLocalAreAnsweredLocally(ModelPreference preference)
    {
        ReadyDocument(Document);
        ReplyWith("NONE");

        var result = await _service.ExtractAsync(_id, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_CloudOff_CloudIsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(Document);

        var result = await _service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        result.Error.Should().Be(CloudNotAvailable);
        NoModelWasCalled();
    }

    [Fact]
    public async Task ExtractAsync_PrivateDocumentWithCloud_IsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(Document, isPrivate: true);

        var result = await _service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        result.Error.Should().Be(PrivateRefused);
        NoModelWasCalled();
    }

    [Fact]
    public async Task ExtractAsync_TheChecksOfTheDocumentComeBeforeRouting_UnknownDocument()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var result = await _service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotFound);
    }

    [Fact]
    public async Task ExtractAsync_TheChecksOfTheDocumentComeBeforeRouting_NotReady()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Failed });

        var result = await _service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotReady);
    }

    [Fact]
    public async Task ExtractAsync_TheChecksOfTheDocumentComeBeforeRouting_TooLittleText()
    {
        ReadyDocument(new string('a', 49));

        var result = await _service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
    }

    [Fact]
    public async Task ExtractAsync_CloudOn_WithTheDefaultThreshold_ALongDocumentUnderAutoStaysLocal()
    {
        ReadyDocument(LongText);
        ReplyWith("NONE");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true); // threshold 2, score at most 1

        var result = await service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_CloudOn_WithThresholdZero_ALongDocumentUnderAutoGoesToTheCloud()
    {
        ReadyDocument(LongText);
        AnswerFromCloudWith("NONE");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
    }

    [Fact]
    public async Task ExtractAsync_CloudOn_WithThresholdZero_AShortDocumentUnderAutoStaysLocal()
    {
        ReadyDocument(ShortText);
        ReplyWith("NONE");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
    }

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task ExtractAsync_CloudOn_APrivateDocumentNeverReachesTheCloud(ModelPreference preference)
    {
        ReadyDocument(LongText, isPrivate: true);
        ReplyWith("NONE");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 }, cloudConfigured: true);

        var result = await service.ExtractAsync(_id, preference, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_CloudOn_CloudOnANonPrivateDocumentReachesTheCloudAndItsReplyIsCheckedLikeALocalOne()
    {
        ReadyDocument(Document);
        AnswerFromCloudWith("person: Mario Rossi\nperson: Luca Verdi");
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

        var result = await service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Cloud);
        result.Groups.Should().ContainSingle();
        result.Groups[0].Entities.Should().Equal("Mario Rossi"); // Luca Verdi is not in the document
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "The cloud model is not available. Check the cloud settings and your connection.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The cloud model did not answer in time. Please try again.")]
    public async Task ExtractAsync_CloudFails_KeepsItsTextAndNeverFallsBackToTheLocalModel(
        AiFailureKind kind, AskFailureKind expected, string message)
    {
        ReadyDocument(ShortText);
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));
        var service = ServiceWith(new RoutingOptions { CloudEnabled = true }, cloudConfigured: true);

        var result = await service.ExtractAsync(_id, ModelPreference.Cloud, CancellationToken.None);

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

    // SC-004: private yes/no x preference x cloud state x score, against an oracle written apart from the router.
    // The threshold is 0, so the score of an extraction (0 for a short text, 1 for a long one) is what Auto looks at.
    [Theory]
    [MemberData(nameof(WholeTable))]
    public async Task ExtractAsync_WholeTable_GivesWhatTheRouterGivesForAQuestion(
        bool isPrivate, ModelPreference preference, ModelRouterTests.CloudState state, bool longText)
    {
        ReadyDocument(longText ? LongText : ShortText, isPrivate);
        ReplyWith("NONE");
        AnswerFromCloudWith("NONE");
        var enabled = state != ModelRouterTests.CloudState.Off;
        var configured = state != ModelRouterTests.CloudState.OnNotConfigured;
        var service = ServiceWith(new RoutingOptions { CloudEnabled = enabled, ComplexityThreshold = 0 }, configured);

        var result = await service.ExtractAsync(_id, preference, CancellationToken.None);

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

    // ---- A long document: only the first part is read, and an entity from the rest is never shown ----

    // The first 100 characters (the limit in these tests) hold Mario Rossi; Luca Verdi comes after the cut.
    private static readonly string LongDocument =
        "Mario Rossi signed here." + new string('.', 76) + " Luca Verdi arrived later, after the cut.";

    [Fact]
    public async Task ExtractAsync_ATextLongerThanTheLimit_IsTruncatedAndTheModelNeverSeesTheEnd()
    {
        ReadyDocument(LongDocument);
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("NONE"));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Truncated.Should().BeTrue();
        sent!.Messages.Should().NotContain(m => m.Content.Contains("Luca Verdi"));
        sent.Messages[0].Content.Should().Contain("Mario Rossi signed here.");
    }

    [Fact]
    public async Task ExtractAsync_AnEntityThatIsInTheDocumentOnlyAfterTheCutIsDroppedEvenIfTheModelNamesIt()
    {
        ReadyDocument(LongDocument);
        ReplyWith("person: Mario Rossi\nperson: Luca Verdi");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Groups.Should().ContainSingle();
        result.Groups[0].Entities.Should().Equal("Mario Rossi");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(60)]
    public async Task ExtractAsync_ATextAtOrUnderTheLimit_IsNotTruncated(int length)
    {
        ReadyDocument(new string('a', length));
        ReplyWith("NONE");

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractAsync_TheCutNeverEndsOnHalfOfACharacter()
    {
        ReadyDocument(new string('a', 99) + "\U0001F600" + "tail-of-the-text"); // a surrogate pair across the limit of 100
        AiRequest? sent = null;
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(AiResult.Success("NONE"));

        var result = await _service.ExtractAsync(_id, ModelPreference.Auto, CancellationToken.None);

        result.Truncated.Should().BeTrue();
        var content = sent!.Messages[0].Content;
        content.Should().NotContain("\U0001F600");
        content.Should().NotContain("tail-of-the-text");
        content.Any(char.IsLowSurrogate).Should().BeFalse();
    }

    private DocumentEntityService ServiceWith(RoutingOptions routing, bool cloudConfigured)
    {
        _cloud.SetupGet(c => c.IsConfigured).Returns(cloudConfigured);
        var router = new ModelRouter(_provider.Object, _cloud.Object, routing);
        return new DocumentEntityService(_repository.Object, router, new ComplexityScorer(Options), new EntityPromptBuilder(Options));
    }

    private void AnswerFromCloudWith(string text) =>
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(text));

    private void NoModelWasCalled()
    {
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void ReplyWith(string text) =>
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(text));

    private void ReadyDocument(string text, bool isPrivate = false)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, IsPrivate = isPrivate });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }
}
