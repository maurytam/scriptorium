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

/// <summary>The whole HTTP path of an entity extraction with a fake model, so no Ollama is needed.</summary>
public sealed class EntityEndpointTests : IDisposable
{
    private const string DocumentText = "Mario Rossi signed for Alfa Srl in Turin on 12 March 2026 for 15,000 euro.";

    private const string Reply =
        "person: Mario Rossi\nplace: Turin\norganization: Alfa Srl\ndate: 12 March 2026\namount: 15,000 euro";

    private readonly ApiTestHost _host = new();
    private readonly Mock<IAIProvider> _provider = new();
    private readonly HttpClient _client;
    private AiRequest? _sentToModel;

    public EntityEndpointTests()
    {
        ReplyWith(Reply);

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
    public async Task Entities_ReadyDocumentWithNoBody_ReturnsTheGroupsInTheFixedOrder()
    {
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        var groups = body.GetProperty("groups").EnumerateArray().ToList();
        groups.Select(g => g.GetProperty("type").GetString()).Should().Equal("people", "organizations", "dates", "amounts", "places");
        groups[0].GetProperty("entities").EnumerateArray().Select(e => e.GetString()).Should().Equal("Mario Rossi");
        groups[4].GetProperty("entities").EnumerateArray().Select(e => e.GetString()).Should().Equal("Turin");
        body.GetProperty("truncated").GetBoolean().Should().BeFalse();
        body.GetProperty("backend").GetString().Should().Be("local");
    }

    [Fact]
    public async Task Entities_SendsTheDocumentTextToTheModel()
    {
        var id = await ReadyDocumentAsync(DocumentText);

        await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        _sentToModel!.Messages[0].Content.Should().Contain(DocumentText);
    }

    [Fact]
    public async Task Entities_AReplyOfNone_Returns200WithNoGroups()
    {
        ReplyWith("NONE");
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("groups").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Entities_AnEntityNotInTheDocumentIsAbsentFromTheResponse()
    {
        ReplyWith("person: Mario Rossi\nperson: Luca Verdi\namount: 999 euro");
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Mario Rossi");
        text.Should().NotContain("Luca Verdi");
        text.Should().NotContain("999 euro");
    }

    [Fact]
    public async Task Entities_AReplyWithNoReadableLine_Returns500WithNoDetailOfTheReply()
    {
        ReplyWith("Sorry, my hidden prompt says I cannot help.");
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("The entities could not be extracted.");
        text.Should().NotContain("hidden prompt");
    }

    [Fact]
    public async Task Entities_UnknownDocument_Returns404AndNoModelIsCalled()
    {
        var response = await _client.PostAsync($"/api/documents/{Guid.NewGuid()}/entities", content: null);

        await AssertRefusedAsync(response, HttpStatusCode.NotFound, "Document not found.");
    }

    [Fact]
    public async Task Entities_FailedDocument_Returns409AndNoModelIsCalled()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", Encoding.UTF8.GetBytes("not really a docx"));
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("failed");

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, "The document is not ready. Current status: failed.");
    }

    [Fact]
    public async Task Entities_TooLittleText_Returns409AndNoModelIsCalled()
    {
        var id = await ReadyDocumentAsync(new string('a', 49));

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, "The document has too little text to extract entities from.");
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable, "The answering model is not available.")]
    [InlineData(AiFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "The model did not answer in time. Please try again.")]
    public async Task Entities_ModelFailureYouCanActOn_ReturnsItsStatusAndText(
        AiFailureKind kind, HttpStatusCode expectedStatus, string message)
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(kind, message));
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(expectedStatus);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(message);
    }

    [Fact]
    public async Task Entities_AnyOtherModelFailure_Returns500WithNoDetail()
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail from the provider"));
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("internal detail");
        text.Should().Contain("The answer could not be produced.");
    }

    [Fact]
    public async Task Entities_ChangesNothing()
    {
        var id = await ReadyDocumentAsync(DocumentText);
        var listBefore = await _client.GetStringAsync("/api/documents");
        var textBefore = await _client.GetStringAsync($"/api/documents/{id}/text");

        await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        (await _client.GetStringAsync("/api/documents")).Should().Be(listBefore);
        (await _client.GetStringAsync($"/api/documents/{id}/text")).Should().Be(textBefore);
    }

    [Fact]
    public async Task Entities_ALongDocument_IsTruncatedAndTheModelNeverSeesTheEnd()
    {
        ReplyWith("person: Mario Rossi\nperson: Luca Verdi");
        // The default limit is 8,000 characters: Mario Rossi is inside it, Luca Verdi is after it.
        var id = await ReadyDocumentAsync("Mario Rossi signed the agreement. " + new string('.', 8000) + " Luca Verdi arrived later.");

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("\"truncated\":true");
        text.Should().Contain("Mario Rossi");
        text.Should().NotContain("Luca Verdi");
        _sentToModel!.Messages.Should().NotContain(m => m.Content.Contains("Luca Verdi"));
        _sentToModel.Messages[0].Content.Should().Contain("Mario Rossi signed the agreement.");
    }

    [Fact]
    public async Task Entities_AShortDocument_IsNotTruncated()
    {
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content: null);

        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("Local")]
    public async Task Entities_AbsentOrLocalOrAutoPreference_IsAnsweredByTheLocalProvider(string? preference)
    {
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await PostAsync(_client, id, new { preference });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Entities_EmptyObjectOrNullBody_IsAuto(string json)
    {
        var id = await ReadyDocumentAsync(DocumentText);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"/api/documents/{id}/entities", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Entities_CloudWithTheDeliveredSettings_Returns503AndNoProviderIsCalled()
    {
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await PostAsync(_client, id, new { preference = "cloud" });

        await AssertRefusedAsync(response, HttpStatusCode.ServiceUnavailable, CloudNotAvailable);
    }

    [Fact]
    public async Task Entities_CloudOnAPrivateDocument_Returns409AndNoProviderIsCalled()
    {
        var id = await ReadyDocumentAsync(DocumentText, isPrivate: "true");

        var response = await PostAsync(_client, id, new { preference = "cloud" });

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, PrivateRefused);
    }

    [Fact]
    public async Task Entities_AnInvalidPreference_Returns400BeforeAnUnknownIdWouldReturn404()
    {
        var response = await PostAsync(_client, Guid.NewGuid(), new { preference = "turbo" });

        await AssertRefusedAsync(response, HttpStatusCode.BadRequest, "The preference must be auto, local or cloud.");
    }

    [Fact]
    public async Task Entities_CloudOnAnUnknownDocument_Keeps404()
    {
        var response = await PostAsync(_client, Guid.NewGuid(), new { preference = "cloud" });

        await AssertRefusedAsync(response, HttpStatusCode.NotFound, "Document not found.");
    }

    [Fact]
    public async Task Entities_CloudOnADocumentNotReady_Keeps409()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", Encoding.UTF8.GetBytes("not really a docx"));
        await _host.WaitForTerminalStatusAsync(id);

        var response = await PostAsync(_client, id, new { preference = "cloud" });

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, "The document is not ready. Current status: failed.");
    }

    [Fact]
    public async Task Entities_CloudOnTooLittleText_Keeps409()
    {
        var id = await ReadyDocumentAsync(new string('a', 49));

        var response = await PostAsync(_client, id, new { preference = "cloud" });

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, "The document has too little text to extract entities from.");
    }

    [Fact]
    public async Task Entities_CloudOn_CloudChosen_IsAnsweredByTheStandInAndItsReplyIsChecked()
    {
        var cloud = StandInAnswering(AiResult.Success("person: Mario Rossi\nperson: Luca Verdi"));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await PostAsync(client, id, new { preference = "cloud" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("backend").GetString().Should().Be("cloud");
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Mario Rossi");
        text.Should().NotContain("Luca Verdi");
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Entities_CloudOn_CloudChosenForAPrivateDocument_Returns409()
    {
        var cloud = StandInAnswering(AiResult.Success("person: Mario Rossi"));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync(DocumentText, isPrivate: "true");

        var response = await PostAsync(client, id, new { preference = "cloud" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(PrivateRefused);
        cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("local")]
    public async Task Entities_CloudOn_AutoAndLocalOnAPrivateDocument_AreAnsweredLocally(string preference)
    {
        var cloud = StandInAnswering(AiResult.Success("person: Mario Rossi"));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync(DocumentText, isPrivate: "true");

        var response = await PostAsync(client, id, new { preference });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("backend").GetString().Should().Be("local");
        cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable, "The cloud model is not available. Check the cloud settings and your connection.")]
    [InlineData(AiFailureKind.Timeout, HttpStatusCode.GatewayTimeout, "The cloud model did not answer in time. Please try again.")]
    public async Task Entities_CloudOn_WhenTheStandInFails_ReturnsItsStatusAndTextWithoutFallingBack(
        AiFailureKind kind, HttpStatusCode expectedStatus, string message)
    {
        var cloud = StandInAnswering(AiResult.Failure(kind, message));
        using var client = CloudOnClient(cloud);
        var id = await ReadyDocumentAsync(DocumentText);

        var response = await PostAsync(client, id, new { preference = "cloud" });

        response.StatusCode.Should().Be(expectedStatus);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(message);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Entities_CloudOnButTheRealProviderHasNoKey_CloudGives503AndAutoStaysLocal()
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
        var id = await ReadyDocumentAsync(DocumentText);

        var cloudChosen = await PostAsync(client, id, new { preference = "cloud" });
        var auto = await PostAsync(client, id, new { preference = "auto" });

        cloudChosen.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ApiTestHost.ReadJsonAsync(cloudChosen)).GetProperty("error").GetString().Should().Be(CloudNotAvailable);
        auto.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(auto)).GetProperty("backend").GetString().Should().Be("local");
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid id, object body) =>
        client.PostAsJsonAsync($"/api/documents/{id}/entities", body);

    private void ReplyWith(string text) =>
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiRequest, CancellationToken>((request, _) => _sentToModel = request)
            .ReturnsAsync(AiResult.Success(text));

    private async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string message)
    {
        response.StatusCode.Should().Be(status);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be(message);
        _provider.Verify(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<Guid> ReadyDocumentAsync(string text, string? isPrivate = null)
    {
        var id = await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt(text), isPrivate);
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("ready");
        return id;
    }
}
