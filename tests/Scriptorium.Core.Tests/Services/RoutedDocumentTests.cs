using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class RoutedDocumentTests
{
    private const string TooLittle = "The document has too little text for this test.";

    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    private static readonly QaOptions Options = new() { MaxDocumentCharacters = 100 };
    private static readonly AiRequest SomeRequest = new([new AiMessage(AiRole.User, "go")]);

    // 75 % of the limit of 100 in these tests: the one signal a routed document can raise (a score of 1).
    private static readonly string LongText = new('a', 80);
    private static readonly string ShortText = new('a', 60);

    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IDocumentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAIProvider> _local = new(MockBehavior.Strict);
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);

    // ---- PrepareAsync: the checks, in order ----

    [Fact]
    public async Task PrepareAsync_UnknownDocument_IsNotFoundAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

        var routed = await PrepareAsync(ModelPreference.Auto);

        routed.IsSuccess.Should().BeFalse();
        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.NotFound);
        NoModelWasCalled();
    }

    [Fact]
    public async Task PrepareAsync_DocumentNotReady_IsNotReadyTheTextIsNotReadAndNoModelIsCalled()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Processing });

        var routed = await PrepareAsync(ModelPreference.Auto);

        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.NotReady);
        _repository.Verify(r => r.GetExtractedTextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        NoModelWasCalled();
    }

    [Fact]
    public async Task PrepareAsync_49Characters_IsNotEnoughTextWithTheMessagePassedInAndNoRouting()
    {
        ReadyDocument(new string('a', 49));

        var routed = await PrepareAsync(ModelPreference.Cloud, CloudOn());

        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.NotEnoughText);
        FailureOf(routed).Error.Should().Be(TooLittle);
        NoModelWasCalled();
    }

    [Fact]
    public async Task PrepareAsync_50Characters_AreEnough()
    {
        ReadyDocument(new string('a', 50));

        var routed = await PrepareAsync(ModelPreference.Auto);

        routed.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareAsync_SpacesAroundTheTextDoNotCount()
    {
        ReadyDocument(new string(' ', 50) + "short text" + new string(' ', 50));

        var routed = await PrepareAsync(ModelPreference.Auto);

        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.NotEnoughText);
    }

    [Fact]
    public async Task PrepareAsync_ASuccessKeepsTheTextOfTheDocument()
    {
        ReadyDocument(ShortText);

        var routed = await PrepareAsync(ModelPreference.Auto);

        routed.Text.Should().Be(ShortText);
    }

    // ---- PrepareAsync: routing ----

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task PrepareAsync_CloudOff_AutoAndLocalPickTheLocalProvider(ModelPreference preference)
    {
        ReadyDocument(ShortText);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("local"));

        var routed = await PrepareAsync(preference);
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareAsync_CloudOff_CloudIsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(ShortText);

        var routed = await PrepareAsync(ModelPreference.Cloud);

        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        FailureOf(routed).Error.Should().Be(CloudNotAvailable);
        NoModelWasCalled();
    }

    [Fact]
    public async Task PrepareAsync_PrivateDocumentWithCloud_IsRefusedAndNoModelIsCalled()
    {
        ReadyDocument(ShortText, isPrivate: true);

        var routed = await PrepareAsync(ModelPreference.Cloud);

        FailureOf(routed).FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        FailureOf(routed).Error.Should().Be(PrivateRefused);
        NoModelWasCalled();
    }

    [Fact]
    public async Task PrepareAsync_TheChecksOfTheDocumentComeBeforeRouting()
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);
        var unknown = await PrepareAsync(ModelPreference.Cloud);

        _repository.Reset();
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Failed });
        var notReady = await PrepareAsync(ModelPreference.Cloud);

        _repository.Reset();
        ReadyDocument(new string('a', 49));
        var tooShort = await PrepareAsync(ModelPreference.Cloud);

        FailureOf(unknown).FailureKind.Should().Be(AskFailureKind.NotFound);
        FailureOf(notReady).FailureKind.Should().Be(AskFailureKind.NotReady);
        FailureOf(tooShort).FailureKind.Should().Be(AskFailureKind.NotEnoughText);
    }

    [Fact]
    public async Task PrepareAsync_CloudOn_WithTheDefaultThreshold_ALongTextUnderAutoStaysLocal()
    {
        ReadyDocument(LongText);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("local"));

        var routed = await PrepareAsync(ModelPreference.Auto, CloudOn()); // threshold 2, score at most 1
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
    }

    [Fact]
    public async Task PrepareAsync_CloudOn_WithThresholdZero_ALongTextGoesToTheCloudAndAShortOneStaysLocal()
    {
        var routing = new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 };
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("cloud"));
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("local"));

        ReadyDocument(LongText);
        var longRouted = await PrepareAsync(ModelPreference.Auto, routing, cloudConfigured: true);
        var longResult = await longRouted.CompleteAsync(SomeRequest, false, CancellationToken.None);

        _repository.Reset();
        ReadyDocument(ShortText);
        var shortRouted = await PrepareAsync(ModelPreference.Auto, routing, cloudConfigured: true);
        var shortResult = await shortRouted.CompleteAsync(SomeRequest, false, CancellationToken.None);

        longResult.Backend.Should().Be(ModelBackend.Cloud);
        shortResult.Backend.Should().Be(ModelBackend.Local);
    }

    [Theory]
    [InlineData(ModelPreference.Auto)]
    [InlineData(ModelPreference.Local)]
    public async Task PrepareAsync_CloudOn_APrivateDocumentNeverReachesTheCloud(ModelPreference preference)
    {
        ReadyDocument(LongText, isPrivate: true);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("local"));
        var routing = new RoutingOptions { CloudEnabled = true, ComplexityThreshold = 0 };

        var routed = await PrepareAsync(preference, routing, cloudConfigured: true);
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.Backend.Should().Be(ModelBackend.Local);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- CompleteAsync ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteAsync_ReturnsTheReplyWithTheTruncatedFlagPassedIn(bool truncated)
    {
        ReadyDocument(ShortText);
        _local.Setup(p => p.CompleteAsync(SomeRequest, It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success("the reply"));

        var routed = await PrepareAsync(ModelPreference.Auto);
        var result = await routed.CompleteAsync(SomeRequest, truncated, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Answer.Should().Be("the reply");
        result.Truncated.Should().Be(truncated);
        result.Backend.Should().Be(ModelBackend.Local);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable, "Ollama is not running.")]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout, "The model took too long.")]
    public async Task CompleteAsync_AFailureYouCanActOn_KeepsTheProvidersText(AiFailureKind kind, AskFailureKind expected, string message)
    {
        ReadyDocument(ShortText);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Failure(kind, message));

        var routed = await PrepareAsync(ModelPreference.Auto);
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be(message);
    }

    [Fact]
    public async Task CompleteAsync_AnyOtherFailure_IsInternalWithNoDetail()
    {
        ReadyDocument(ShortText);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail"));

        var routed = await PrepareAsync(ModelPreference.Auto);
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task CompleteAsync_ABlankReply_IsInternalNeverAnEmptyResult(string blank)
    {
        ReadyDocument(ShortText);
        _local.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(AiResult.Success(blank));

        var routed = await PrepareAsync(ModelPreference.Auto);
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, AskFailureKind.ModelUnavailable)]
    [InlineData(AiFailureKind.Timeout, AskFailureKind.Timeout)]
    public async Task CompleteAsync_ACloudFailure_IsReportedAndTheLocalProviderIsNeverCalled(AiFailureKind kind, AskFailureKind expected)
    {
        ReadyDocument(ShortText);
        _cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, "cloud said no"));

        var routed = await PrepareAsync(ModelPreference.Cloud, CloudOn());
        var result = await routed.CompleteAsync(SomeRequest, false, CancellationToken.None);

        result.FailureKind.Should().Be(expected);
        result.Error.Should().Be("cloud said no");
        _local.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheCancellationTokenReachesTheRepositoryAndTheProvider()
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(r => r.GetByIdAsync(_id, cancellation.Token))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, cancellation.Token))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = ShortText });
        _local.Setup(p => p.CompleteAsync(SomeRequest, cancellation.Token)).ReturnsAsync(AiResult.Success("ok"));
        _cloud.SetupGet(c => c.IsConfigured).Returns(false);
        var router = new ModelRouter(_local.Object, _cloud.Object, new RoutingOptions());

        var routed = await RoutedDocument.PrepareAsync(
            _repository.Object, router, new ComplexityScorer(Options), _id, ModelPreference.Auto, TooLittle, cancellation.Token);
        var result = await routed.CompleteAsync(SomeRequest, false, cancellation.Token);

        result.IsSuccess.Should().BeTrue();
        _repository.VerifyAll();
        _local.VerifyAll();
    }

    // The cloud is switched on with the delivered threshold of 2; PrepareAsync then gives it a configured stand-in.
    private static RoutingOptions CloudOn() => new() { CloudEnabled = true };

    // A refused document carries its failure; this reads it without a null-forgiving operator.
    private static AskResult FailureOf(RoutedDocument routed)
    {
        routed.IsSuccess.Should().BeFalse();
        return routed.Failure ?? throw new InvalidOperationException("The prepared document has no failure.");
    }

    private Task<RoutedDocument> PrepareAsync(ModelPreference preference, RoutingOptions? routing = null, bool cloudConfigured = false)
    {
        // An options object that switches the cloud on also means a configured stand-in, unless the test says otherwise.
        routing ??= new RoutingOptions();
        cloudConfigured |= routing.CloudEnabled;
        _cloud.SetupGet(c => c.IsConfigured).Returns(cloudConfigured);
        var router = new ModelRouter(_local.Object, _cloud.Object, routing);

        return RoutedDocument.PrepareAsync(
            _repository.Object, router, new ComplexityScorer(Options), _id, preference, TooLittle, CancellationToken.None);
    }

    private void NoModelWasCalled()
    {
        _local.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void ReadyDocument(string text, bool isPrivate = false)
    {
        _repository.Setup(r => r.GetByIdAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = _id, Status = DocumentStatus.Ready, IsPrivate = isPrivate });
        _repository.Setup(r => r.GetExtractedTextAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedText { DocumentId = _id, Content = text });
    }
}
