using System.Net;
using FluentAssertions;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class DocumentTextPreviewEndpointTests : IDisposable
{
    private const int LongTextLength = 9000; // above the default limit of 8,000 characters
    private const int DefaultLimit = 8000;

    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task GetTextPreview_UnknownId_ReturnsNotFound()
    {
        var response = await _host.Client.GetAsync($"/api/documents/{Guid.NewGuid()}/text/preview");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTextPreview_FailedDocument_ReturnsConflictNamingTheStatus()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", SampleDocuments.Txt("not a docx"));
        await _host.WaitForTerminalStatusAsync(id);

        var response = await _host.Client.GetAsync($"/api/documents/{id}/text/preview");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("Document is not ready. Current status: failed.");
    }

    [Fact]
    public async Task GetTextPreview_ShortText_ReturnsItWholeAndNotTruncated()
    {
        var id = await UploadReadyTextAsync("short.txt", "Scriptorium sample text");

        var response = await _host.Client.GetAsync($"/api/documents/{id}/text/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("documentId").GetGuid().Should().Be(id);
        body.GetProperty("content").GetString().Should().Be("Scriptorium sample text");
        body.GetProperty("truncated").GetBoolean().Should().BeFalse();
        body.GetProperty("totalCharacters").GetInt32().Should().Be("Scriptorium sample text".Length);
    }

    [Fact]
    public async Task GetTextPreview_LongText_ReturnsTheFirstCharactersOfTheFullTextAndTheTotal()
    {
        var id = await UploadReadyTextAsync("long.txt", LongText());
        var full = await FullTextAsync(id);

        var response = await _host.Client.GetAsync($"/api/documents/{id}/text/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ApiTestHost.ReadJsonAsync(response);
        body.GetProperty("truncated").GetBoolean().Should().BeTrue();
        body.GetProperty("totalCharacters").GetInt32().Should().Be(full.Length);
        var content = body.GetProperty("content").GetString();
        content.Should().HaveLength(DefaultLimit);
        full.Should().StartWith(content);
    }

    [Fact]
    public async Task GetText_LongText_StillReturnsTheWholeText()
    {
        var id = await UploadReadyTextAsync("long.txt", LongText());

        var full = await FullTextAsync(id);

        full.Length.Should().BeGreaterThan(DefaultLimit); // the parser may trim a trailing line break, so no exact length
    }

    private async Task<Guid> UploadReadyTextAsync(string fileName, string text)
    {
        var id = await _host.UploadAcceptedAsync(fileName, SampleDocuments.Txt(text));
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("ready");
        return id;
    }

    private async Task<string> FullTextAsync(Guid id)
    {
        var response = await _host.Client.GetAsync($"/api/documents/{id}/text");
        return (await ApiTestHost.ReadJsonAsync(response)).GetProperty("content").GetString()!; // /text of a ready document always has content
    }

    private static string LongText() =>
        string.Concat(Enumerable.Range(0, LongTextLength / 10 + 1).Select(i => $"line{i:D5}\n"))[..LongTextLength];
}
