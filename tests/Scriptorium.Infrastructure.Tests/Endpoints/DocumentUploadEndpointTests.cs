using System.Net;
using FluentAssertions;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class DocumentUploadEndpointTests : IDisposable
{
    private const int OneMegabyte = 1024 * 1024;

    private readonly ApiTestHost _host = new();

    public void Dispose() => _host.Dispose();

    public static TheoryData<string, string> SupportedFiles => new()
    {
        { "report.pdf", "pdf" },
        { "minutes.docx", "docx" },
        { "budget.xlsx", "xlsx" },
        { "notes.txt", "txt" }
    };

    [Theory]
    [MemberData(nameof(SupportedFiles))]
    public async Task Upload_SupportedFile_ReachesReadyAndExposesExtractedText(string fileName, string fileType)
    {
        var response = await _host.UploadAsync(fileName, SampleFor(fileType, "Scriptorium sample"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await ApiTestHost.ReadJsonAsync(response);
        accepted.GetProperty("status").GetString().Should().Be("processing");
        accepted.GetProperty("fileType").GetString().Should().Be(fileType);

        var id = accepted.GetProperty("id").GetGuid();
        var details = await _host.WaitForTerminalStatusAsync(id);
        details.GetProperty("status").GetString().Should().Be("ready");

        var text = await _host.Client.GetAsync($"/api/documents/{id}/text");
        text.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(text)).GetProperty("content").GetString().Should().Contain("Scriptorium sample");
    }

    [Fact]
    public async Task Upload_WithoutFile_ReturnsBadRequest()
    {
        var response = await _host.Client.PostAsync("/api/documents", new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var response = await _host.Client.GetAsync($"/api/documents/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetText_UnknownId_ReturnsNotFound()
    {
        var response = await _host.Client.GetAsync($"/api/documents/{Guid.NewGuid()}/text");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_UnsupportedType_ReturnsBadRequestNamingSupportedFormats()
    {
        var response = await _host.UploadAsync("photo.png", [1, 2, 3]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("Unsupported file type '.png'. Supported types: pdf, docx, xlsx, txt.");
        _host.StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_FileOverConfiguredLimit_ReturnsPayloadTooLargeAndStoresNothing()
    {
        using var smallLimitFactory = _host.Factory.WithWebHostBuilder(
            builder => builder.UseSetting("Documents:MaxSizeBytes", OneMegabyte.ToString()));
        using var client = smallLimitFactory.CreateClient();
        var form = new MultipartFormDataContent { { new ByteArrayContent(new byte[OneMegabyte + 1]), "file", "big.txt" } };

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("File exceeds the maximum size of 1 MB.");
        _host.StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_CorruptedDocx_IsAcceptedThenFailedWithReasonAndApiStaysResponsive()
    {
        var response = await _host.UploadAsync("corrupted.docx", SampleDocuments.Txt("this is a renamed text file"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var id = (await ApiTestHost.ReadJsonAsync(response)).GetProperty("id").GetGuid();
        var details = await _host.WaitForTerminalStatusAsync(id);
        details.GetProperty("status").GetString().Should().Be("failed");
        details.GetProperty("failureReason").GetString().Should().Contain("Corrupted");

        var next = await _host.UploadAsync("after.txt", SampleDocuments.Txt("still working"));
        var nextId = (await ApiTestHost.ReadJsonAsync(next)).GetProperty("id").GetGuid();
        (await _host.WaitForTerminalStatusAsync(nextId)).GetProperty("status").GetString().Should().Be("ready");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public async Task Upload_IsPrivateField_RoundTripsThroughGetById(string? field, bool expected)
    {
        var response = await _host.UploadAsync("notes.txt", SampleDocuments.Txt("secret"), field);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await ApiTestHost.ReadJsonAsync(response);
        accepted.GetProperty("isPrivate").GetBoolean().Should().Be(expected);

        var id = accepted.GetProperty("id").GetGuid();
        var details = await ApiTestHost.ReadJsonAsync(await _host.Client.GetAsync($"/api/documents/{id}"));
        details.GetProperty("isPrivate").GetBoolean().Should().Be(expected);
    }

    [Fact]
    public async Task Upload_InvalidIsPrivateValue_ReturnsBadRequest()
    {
        var response = await _host.UploadAsync("notes.txt", SampleDocuments.Txt("x"), "maybe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _host.StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_SameContentAgain_IsRejectedWithConflictNamingTheOriginal()
    {
        var content = SampleDocuments.Txt("identical content");
        var firstId = await _host.UploadAcceptedAsync("original.txt", content);

        var again = await _host.UploadAsync("original.txt", content);
        var renamed = await _host.UploadAsync("renamed-copy.txt", content);

        foreach (var response in new[] { again, renamed })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
                .Should().Be("This file has already been uploaded as 'original.txt'.");
        }

        _host.StoredDocumentFolders().Should().ContainSingle();
        (await ApiTestHost.ReadJsonAsync(await _host.Client.GetAsync("/api/documents"))).GetArrayLength().Should().Be(1);
        (await _host.WaitForTerminalStatusAsync(firstId)).GetProperty("status").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task Upload_SameNameButDifferentContent_IsAccepted()
    {
        await _host.UploadAcceptedAsync("notes.txt", SampleDocuments.Txt("version one"));

        var second = await _host.UploadAsync("notes.txt", SampleDocuments.Txt("version two"));

        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Upload_SameContentAsBefore_IsAcceptedAgainAfterTheOriginalWasDeleted()
    {
        var content = SampleDocuments.Txt("delete me then upload me");
        var id = await _host.UploadAcceptedAsync("first.txt", content);
        await _host.WaitForTerminalStatusAsync(id);
        (await _host.Client.DeleteAsync($"/api/documents/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await _host.UploadAsync("first.txt", content);

        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetLimits_ReturnsConfiguredMaxSize()
    {
        using var factory = _host.Factory.WithWebHostBuilder(
            builder => builder.UseSetting("Documents:MaxSizeBytes", OneMegabyte.ToString()));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/documents/limits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("maxSizeBytes").GetInt64().Should().Be(OneMegabyte);
    }

    private static byte[] SampleFor(string fileType, string text) => fileType switch
    {
        "pdf" => SampleDocuments.Pdf(text),
        "docx" => SampleDocuments.Docx(text),
        "xlsx" => SampleDocuments.Xlsx(text),
        _ => SampleDocuments.Txt(text)
    };
}
