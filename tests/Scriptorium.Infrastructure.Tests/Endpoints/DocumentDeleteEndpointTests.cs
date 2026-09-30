using System.Net;
using FluentAssertions;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class DocumentDeleteEndpointTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Delete_ReadyDocument_RemovesItEverywhere()
    {
        var id = await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt("to remove"));
        await _host.WaitForTerminalStatusAsync(id);
        _host.StoredDocumentFolders().Should().ContainSingle();

        var response = await _host.Client.DeleteAsync($"/api/documents/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _host.Client.GetAsync($"/api/documents/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _host.Client.GetAsync($"/api/documents/{id}/text")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiTestHost.ReadJsonAsync(await _host.Client.GetAsync("/api/documents"))).GetArrayLength().Should().Be(0);
        _host.StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_FailedDocument_IsAllowed()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", SampleDocuments.Txt("not a docx"));
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("failed");

        var response = await _host.Client.DeleteAsync($"/api/documents/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_OnlyRemovesTheRequestedDocument()
    {
        var keep = await _host.UploadAcceptedAsync("keep.txt", SampleDocuments.Txt("keep"));
        var remove = await _host.UploadAcceptedAsync("remove.txt", SampleDocuments.Txt("remove"));
        await _host.WaitForTerminalStatusAsync(keep);
        await _host.WaitForTerminalStatusAsync(remove);

        await _host.Client.DeleteAsync($"/api/documents/{remove}");

        var items = (await ApiTestHost.ReadJsonAsync(await _host.Client.GetAsync("/api/documents"))).EnumerateArray().ToList();
        items.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(keep);
        (await _host.Client.GetAsync($"/api/documents/{keep}/text")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_UnknownDocument_ReturnsNotFound()
    {
        var response = await _host.Client.DeleteAsync($"/api/documents/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
