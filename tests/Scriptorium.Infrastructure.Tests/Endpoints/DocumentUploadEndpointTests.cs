using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public sealed class DocumentUploadEndpointTests : IDisposable
{
    private const int OneMegabyte = 1024 * 1024;

    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "scriptorium-tests", Guid.NewGuid().ToString());
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DocumentUploadEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_dataPath, "test.db")}");
                builder.UseSetting("Storage:LocalPath", Path.Combine(_dataPath, "documents"));
            });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

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
        var response = await UploadAsync(fileName, SampleFor(fileType, "Scriptorium sample"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var accepted = await ReadJsonAsync(response);
        accepted.GetProperty("status").GetString().Should().Be("processing");
        accepted.GetProperty("fileType").GetString().Should().Be(fileType);

        var id = accepted.GetProperty("id").GetGuid();
        var details = await WaitForTerminalStatusAsync(id);
        details.GetProperty("status").GetString().Should().Be("ready");

        var text = await _client.GetAsync($"/api/documents/{id}/text");
        text.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(text)).GetProperty("content").GetString().Should().Contain("Scriptorium sample");
    }

    [Fact]
    public async Task Upload_WithoutFile_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/documents", new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/documents/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetText_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/documents/{Guid.NewGuid()}/text");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_UnsupportedType_ReturnsBadRequestNamingSupportedFormats()
    {
        var response = await UploadAsync("photo.png", [1, 2, 3]);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("Unsupported file type '.png'. Supported types: pdf, docx, xlsx, txt.");
        StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_FileOverConfiguredLimit_ReturnsPayloadTooLargeAndStoresNothing()
    {
        using var smallLimitFactory = _factory.WithWebHostBuilder(
            builder => builder.UseSetting("Documents:MaxSizeBytes", OneMegabyte.ToString()));
        using var client = smallLimitFactory.CreateClient();
        var form = new MultipartFormDataContent { { new ByteArrayContent(new byte[OneMegabyte + 1]), "file", "big.txt" } };

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("File exceeds the maximum size of 1 MB.");
        StoredDocumentFolders().Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_CorruptedDocx_IsAcceptedThenFailedWithReasonAndApiStaysResponsive()
    {
        var response = await UploadAsync("corrupted.docx", SampleDocuments.Txt("this is a renamed text file"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var id = (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
        var details = await WaitForTerminalStatusAsync(id);
        details.GetProperty("status").GetString().Should().Be("failed");
        details.GetProperty("failureReason").GetString().Should().Contain("Corrupted");

        var next = await UploadAsync("after.txt", SampleDocuments.Txt("still working"));
        var nextId = (await ReadJsonAsync(next)).GetProperty("id").GetGuid();
        (await WaitForTerminalStatusAsync(nextId)).GetProperty("status").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task GetLimits_ReturnsConfiguredMaxSize()
    {
        using var factory = _factory.WithWebHostBuilder(
            builder => builder.UseSetting("Documents:MaxSizeBytes", OneMegabyte.ToString()));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/documents/limits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("maxSizeBytes").GetInt64().Should().Be(OneMegabyte);
    }

    private string[] StoredDocumentFolders()
    {
        var documents = Path.Combine(_dataPath, "documents");
        return Directory.Exists(documents) ? Directory.GetDirectories(documents) : [];
    }

    private static byte[] SampleFor(string fileType, string text) => fileType switch
    {
        "pdf" => SampleDocuments.Pdf(text),
        "docx" => SampleDocuments.Docx(text),
        "xlsx" => SampleDocuments.Xlsx(text),
        _ => SampleDocuments.Txt(text)
    };

    private Task<HttpResponseMessage> UploadAsync(string fileName, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        return _client.PostAsync("/api/documents", form);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<JsonElement> WaitForTerminalStatusAsync(Guid id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var details = await ReadJsonAsync(await _client.GetAsync($"/api/documents/{id}"));
            if (details.GetProperty("status").GetString() != "processing")
            {
                return details;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Document {id} did not reach a terminal status.");
    }
}
