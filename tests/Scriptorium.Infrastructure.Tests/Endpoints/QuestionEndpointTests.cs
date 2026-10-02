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
    public async Task Ask_ModelFailure_ReturnsAGenericServerErrorWithoutDetails()
    {
        _provider.Setup(p => p.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiResult.Failure(AiFailureKind.Failed, "internal detail from the provider"));
        var id = await ReadyDocumentAsync("some text");

        var response = await AskAsync(id, "Who signed?");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("internal detail");
    }

    private async Task<Guid> ReadyDocumentAsync(string text)
    {
        var id = await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt(text));
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("ready");
        return id;
    }

    private Task<HttpResponseMessage> AskAsync(Guid id, string question) =>
        _client.PostAsJsonAsync($"/api/documents/{id}/ask", new { question });
}
