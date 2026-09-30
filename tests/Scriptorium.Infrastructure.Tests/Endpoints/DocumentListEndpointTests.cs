using System.Net;
using FluentAssertions;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class DocumentListEndpointTests : IDisposable
{
    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task List_NoDocuments_ReturnsEmptyArray()
    {
        var response = await _host.Client.GetAsync("/api/documents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task List_ReturnsUploadedDocumentsMostRecentFirstWithMetadata()
    {
        var first = await _host.UploadAcceptedAsync("first.txt", SampleDocuments.Txt("one"));
        var second = await _host.UploadAcceptedAsync("second.docx", SampleDocuments.Docx("two"), isPrivate: "true");
        await _host.WaitForTerminalStatusAsync(first);
        await _host.WaitForTerminalStatusAsync(second);

        var items = (await ListAsync()).EnumerateArray().ToList();

        items.Select(i => i.GetProperty("id").GetGuid()).Should().Equal(second, first);
        items[0].GetProperty("fileName").GetString().Should().Be("second.docx");
        items[0].GetProperty("fileType").GetString().Should().Be("docx");
        items[0].GetProperty("isPrivate").GetBoolean().Should().BeTrue();
        items[0].GetProperty("status").GetString().Should().Be("ready");
        items[0].GetProperty("uploadDate").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5));
        items[1].GetProperty("isPrivate").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task List_ShowsFailedStatusForCorruptedFiles()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", SampleDocuments.Txt("not a docx"));
        await _host.WaitForTerminalStatusAsync(id);

        var items = (await ListAsync()).EnumerateArray().ToList();

        items.Should().ContainSingle().Which.GetProperty("status").GetString().Should().Be("failed");
    }

    [Fact]
    public async Task List_TwoUploadsWithTheSameFileName_AreSeparateEntries()
    {
        var a = await _host.UploadAcceptedAsync("same.txt", SampleDocuments.Txt("a"));
        var b = await _host.UploadAcceptedAsync("same.txt", SampleDocuments.Txt("b"));

        var items = (await ListAsync()).EnumerateArray().ToList();

        items.Select(i => i.GetProperty("id").GetGuid()).Should().BeEquivalentTo([a, b]);
        items.Should().OnlyContain(i => i.GetProperty("fileName").GetString() == "same.txt");
    }

    [Fact]
    public async Task List_ShowsReadyStatusOnceProcessingCompletes()
    {
        var id = await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt("hello"));
        await _host.WaitForTerminalStatusAsync(id);

        var items = (await ListAsync()).EnumerateArray().ToList();

        items.Should().ContainSingle().Which.GetProperty("status").GetString().Should().Be("ready");
    }

    private async Task<System.Text.Json.JsonElement> ListAsync() =>
        await ApiTestHost.ReadJsonAsync(await _host.Client.GetAsync("/api/documents"));
}
