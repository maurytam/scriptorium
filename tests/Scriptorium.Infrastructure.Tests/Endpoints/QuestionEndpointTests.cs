using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

/// <summary>The whole HTTP path with a fake model, so no Ollama is needed.</summary>
public sealed class QuestionEndpointTests : IDisposable
{
    private readonly ApiTestHost _host = new();
    private readonly Mock<IAIProvider> _provider = new();
    private readonly HttpClient _client;
    private AiRequest? _sentToModel;

    public QuestionEndpointTests()
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => _sentToModel = request)
            .ReturnsAsync(AiResult.Success("Maria Rossi."));

        _client = _host.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAIProvider>();
            services.AddSingleton(_provider.Object);
        })).CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task Ask_ReadyDocument_ReturnsTheAnswerBasedOnTheDocumentText()
    {
        var id = await ReadyDocumentAsync("Maria Rossi signed the agreement.");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("answer").GetString().Should().Be("Maria Rossi.");
        body.GetProperty("truncated").GetBoolean().Should().BeFalse();
        _sentToModel!.Messages[0].Content.Should().Contain("Maria Rossi signed the agreement.");
        _sentToModel.Messages[^1].Should().Be(new AiMessage(AiRole.User, "Who signed?"));
    }

    [Fact]
    public async Task Ask_UnknownDocument_ReturnsNotFound()
    {
        var response = await AskAsync(Guid.NewGuid(), "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be("Document not found.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_FailedDocument_ReturnsConflictNamingTheStatus()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", SampleDocuments.Txt("not a docx"));
        await _host.WaitForTerminalStatusAsync(id);

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("The document is not ready. Current status: failed.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Ask_EmptyQuestion_ReturnsBadRequestWithoutCallingTheModel(string question)
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, question);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be("The question is empty.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_NoBody_ReturnsBadRequest()
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await _client.PostAsync($"/api/documents/{id}/ask", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ask_MalformedJson_ReturnsBadRequestInTheJsonErrorShape()
    {
        var id = await ReadyDocumentAsync("some text");
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"/api/documents/{id}/ask", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("error");
    }

    [Fact]
    public async Task Ask_ModelFailure_ReturnsAGenericServerErrorWithoutDetailsAndTheApiKeepsAnswering()
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail from the provider"));
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("internal detail");
        await AssertApiKeepsAnsweringAsync();
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable, "The answering model is not available. Check that Ollama is running and the model is installed.")]
    [InlineData(AiFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "The model did not answer in time. Please try again.")]
    public async Task Ask_ModelNotAvailableOrTooSlow_ReturnsTheStatusAndMessageOfTheContract(
        AiFailureKind kind, HttpStatusCode expectedStatus, string message)
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(expectedStatus);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(message);
        await AssertApiKeepsAnsweringAsync();
    }

    [Fact]
    public async Task Ask_QuestionOverTwoThousandCharacters_ReturnsBadRequestWithoutCallingTheModel()
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, new string('q', 2001));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Contain("2000 characters");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAskLimits_ReturnsTheDefaultQuestionLength()
    {
        var response = await _client.GetAsync("/api/documents/ask/limits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("maxQuestionLength").GetInt32().Should().Be(2000);
    }

    [Fact]
    public async Task GetAskLimits_FollowsTheConfiguredQuestionLength()
    {
        using var factory = _host.Factory.WithWebHostBuilder(
            builder => builder.UseSetting("Qa:MaxQuestionLength", "500"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/documents/ask/limits");

        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("maxQuestionLength").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task Ask_DocumentOverTheBudget_ReturnsTruncatedTrue()
    {
        var id = await ReadyDocumentAsync(new string('x', 9000));

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Ask_WithHistory_ForwardsTheEarlierExchangesToTheModel()
    {
        var id = await ReadyDocumentAsync("Maria Rossi signed the agreement in May.");
        var history = new[] { new { question = "Who signed?", answer = "Maria Rossi." } };

        var response = await _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question = "When?", history });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _sentToModel!.Messages.Skip(1).Should().Equal(
            new AiMessage(AiRole.User, "Who signed?"),
            new AiMessage(AiRole.Assistant, "Maria Rossi."),
            new AiMessage(AiRole.User, "When?"));
    }

    [Fact]
    public async Task Ask_WithAnEmptyHistory_WorksAsWithoutOne()
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question = "q", history = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _sentToModel!.Messages.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("""{"question":"q","history":[{"question":"Who?","answer":""}]}""")]
    [InlineData("""{"question":"q","history":[{"question":"  ","answer":"Maria"}]}""")]
    [InlineData("""{"question":"q","history":[{"question":"Who?"}]}""")]
    [InlineData("""{"question":"q","history":[null]}""")]
    public async Task Ask_AnIncompleteHistoryEntry_ReturnsBadRequestWithoutCallingTheModel(string json)
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await _client.PostAsync($"/api/documents/{id}/ask", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("Every earlier exchange needs a question and an answer.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- routing (feature 005, user story 1): with the delivered settings the cloud is off ----

    [Fact]
    public async Task Ask_WithTheDeliveredSettings_IsAnsweredLocallyAndSaysSo()
    {
        var id = await ReadyDocumentAsync("Maria Rossi signed the agreement.");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("answer").GetString().Should().Be("Maria Rossi.");
        body.GetProperty("backend").GetString().Should().Be("local");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ask_APrivateDocument_IsAnsweredLocallyWithTheSameResult()
    {
        var id = await ReadyDocumentAsync("Confidential: Maria Rossi signed.", isPrivate: "true");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- routing (feature 005, user story 2): the preference sent with the question ----

    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused = "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("Auto")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public async Task Ask_AutoOrLocalPreference_IsAnsweredByTheLocalProvider(string? preference)
    {
        var id = await ReadyDocumentAsync("Maria Rossi signed the agreement.");

        var response = await AskAsync(id, "Who signed?", preference);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("answer").GetString().Should().Be("Maria Rossi.");
        body.GetProperty("backend").GetString().Should().Be("local");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ask_ANullPreference_BehavesAsAuto()
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question = "q", preference = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
    }

    [Theory]
    [InlineData("cloud")]
    [InlineData("CLOUD")]
    public async Task Ask_CloudWhileTheCloudIsOff_Returns503AndNeverCallsTheLocalProvider(string preference)
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "Who signed?", preference);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(CloudNotAvailable);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_CloudOnAPrivateDocument_Returns409AndNoModelIsCalled()
    {
        var id = await ReadyDocumentAsync("Confidential: Maria Rossi signed.", isPrivate: "true");

        var response = await AskAsync(id, "Who signed?", "cloud");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(PrivateRefused);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("turbo")]
    [InlineData("   ")]
    [InlineData("cloud ")]
    public async Task Ask_AnUnknownPreference_Returns400WithoutCallingAnyModel(string preference)
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "Who signed?", preference);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("The preference must be auto, local or cloud.");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_AnInvalidPreference_IsCheckedBeforeAnythingElse()
    {
        var response = await AskAsync(Guid.NewGuid(), string.Empty, "turbo");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("The preference must be auto, local or cloud.");
    }

    [Fact]
    public async Task Ask_WithCloud_AnUnknownDocumentStillGives404()
    {
        var response = await AskAsync(Guid.NewGuid(), "Who signed?", "cloud");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be("Document not found.");
    }

    [Fact]
    public async Task Ask_WithCloud_ADocumentNotReadyStillGives409NamingTheStatus()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", SampleDocuments.Txt("not a docx"), "true");
        await _host.WaitForTerminalStatusAsync(id);

        var response = await AskAsync(id, "Who signed?", "cloud");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("The document is not ready. Current status: failed.");
    }

    [Fact]
    public async Task Ask_WithCloud_AnInvalidQuestionStillGives400()
    {
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "   ", "cloud");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be("The question is empty.");
    }

    // ---- routing (feature 005, user story 3): the cloud switched on by configuration, with a stand-in and no key ----

    // Two question marks, reasoning words and 400+ characters: three signals, above the default threshold of 2.
    private static readonly string ComplexQuestion = "Compare the two clauses? Explain why they differ? " + new string('x', 400);

    [Fact]
    public async Task Ask_CloudOn_AComplexAutoQuestionIsAnsweredByTheStandInAndSaysCloud()
    {
        var cloud = StandInAnswering(AiResult.Success("From the cloud."));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync("Some document text.");

        var response = await AskAsync(client, id, ComplexQuestion, "auto");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("answer").GetString().Should().Be("From the cloud.");
        body.GetProperty("backend").GetString().Should().Be("cloud");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_CloudOn_TheSameQuestionOnAPrivateDocumentIsAnsweredLocally()
    {
        var cloud = StandInAnswering(AiResult.Success("From the cloud."));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync("Confidential text.", isPrivate: "true");

        var response = await AskAsync(client, id, ComplexQuestion, "auto");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
        cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_CloudOn_ASimpleAutoQuestionStaysLocal()
    {
        var cloud = StandInAnswering(AiResult.Success("From the cloud."));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync("Some document text.");

        var response = await AskAsync(client, id, "Who signed?", "auto");

        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
        cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_CloudOnButTheRealProviderHasNoKey_CloudGives503AndAutoStaysLocal()
    {
        using var factory = _host.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Routing:CloudEnabled", "true");
            builder.UseSetting("Anthropic:ApiKey", string.Empty); // a key in the owner's secrets must not change this test
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAIProvider>();
                services.AddSingleton(_provider.Object);
            });
        });
        using var client = factory.CreateClient();
        var id = await ReadyDocumentAsync("Some document text.");

        var cloudChosen = await AskAsync(client, id, "Who signed?", "cloud");
        var autoComplex = await AskAsync(client, id, ComplexQuestion, "auto");

        cloudChosen.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ApiTestHost.ReadJsonAsync(cloudChosen)).GetProperty("error").GetString().Should().Be(CloudNotAvailable);
        autoComplex.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(autoComplex)).GetProperty("backend").GetString().Should().Be("local");
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable, "The cloud model is not available. Check the cloud settings and your connection.")]
    [InlineData(AiFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "The cloud model did not answer in time. Please try again.")]
    public async Task Ask_CloudOn_WhenTheStandInFails_ReturnsItsStatusAndTextWithoutFallingBackToTheLocalModel(
        AiFailureKind kind, HttpStatusCode expectedStatus, string message)
    {
        var cloud = StandInAnswering(AiResult.Failure(kind, message));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync("Some document text.");

        var response = await AskAsync(client, id, "Who signed?", "cloud");

        response.StatusCode.Should().Be(expectedStatus);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(message);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ICloudAIProvider> StandInAnswering(AiResult result)
    {
        var cloud = new Mock<ICloudAIProvider>();
        cloud.SetupGet(c => c.IsConfigured).Returns(true);
        cloud.Setup(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return cloud;
    }

    // The cloud is switched on by configuration and the real provider is replaced by the stand-in: nothing leaves the process.
    private HttpClient CloudOnClient(Mock<ICloudAIProvider> cloud) =>
        _host.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Routing:CloudEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAIProvider>();
                services.AddSingleton(_provider.Object);
                services.RemoveAll<ICloudAIProvider>();
                services.AddSingleton(cloud.Object);
            });
        }).CreateClient();

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, Guid id, string question, string preference) =>
        client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question, preference });

    private async Task<Guid> ReadyDocumentAsync(string text, string? isPrivate = null)
    {
        var id = await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt(text), isPrivate);
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("ready");
        return id;
    }

    private async Task AssertApiKeepsAnsweringAsync() =>
        (await _client.GetAsync("/api/documents/limits")).StatusCode.Should().Be(HttpStatusCode.OK);

    private Task<HttpResponseMessage> AskAsync(Guid id, string question, string? preference = null) =>
        preference is null
            ? _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question })
            : _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question, preference });
}
