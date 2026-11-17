using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Scriptorium.Core.Interfaces;
using Scriptorium.Infrastructure.Tests.Parsing;
using Scriptorium.Infrastructure.Tests.Support;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

/// <summary>The whole HTTP path of reading the signatures of a document. No model is involved, so both providers fail the test if called.</summary>
public sealed class SignatureEndpointTests : IDisposable
{
    private readonly ApiTestHost _host = new();
    private readonly Mock<IAIProvider> _local = new(MockBehavior.Strict);
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);
    private readonly HttpClient _client;

    public SignatureEndpointTests()
    {
        _client = _host.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAIProvider>();
            services.RemoveAll<ICloudAIProvider>();
            services.AddSingleton(_local.Object);
            services.AddSingleton(_cloud.Object);
        })).CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task Signatures_SignedPdf_ReturnsTheEntries()
    {
        var pdf = SignedPdfFixture.Build(
            "Hello",
            new FakeSignature("D:20260314103200+01'00'", SignerName: "Anna Verdi"),
            new FakeSignature(null, SignerName: "Luca Bianchi"));

        var body = await SignaturesOfAsync("signed.pdf", pdf);

        Entries(body).Should().Equal(
            ("Anna Verdi", "2026-03-14 10:32:00 +01:00"),
            ("Luca Bianchi", null));
    }

    [Fact]
    public async Task Signatures_SignedWord_ReturnsTheEntry()
    {
        using var docx = SignedOfficeFixture.Docx("Contract", FakeOfficeSignature.By("Mario Rossi Prova", "2026-03-14T10:32:00+01:00"));

        var body = await SignaturesOfAsync("signed.docx", docx.ToArray());

        Entries(body).Should().Equal(("Mario Rossi Prova", "2026-03-14 10:32:00 +01:00"));
    }

    [Fact]
    public async Task Signatures_SignedExcelWithTwoSignatures_ReturnsBothInOrder()
    {
        using var xlsx = SignedOfficeFixture.Xlsx(
            "Cells",
            FakeOfficeSignature.By("Anna Verdi Prova", "2026-03-14T09:00:00Z"),
            new FakeOfficeSignature());

        var body = await SignaturesOfAsync("signed.xlsx", xlsx.ToArray());

        Entries(body).Should().Equal(("Anna Verdi Prova", "2026-03-14 09:00:00 +00:00"), (null, null));
    }

    [Fact]
    public async Task Signatures_KeepsAMissingSignerAndDateAsJsonNull()
    {
        using var xlsx = SignedOfficeFixture.Xlsx("Cells", new FakeOfficeSignature());

        var body = await SignaturesOfAsync("signed.xlsx", xlsx.ToArray());

        var entry = body.GetProperty("signatures").EnumerateArray().Single();
        entry.GetProperty("signer").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        entry.GetProperty("signedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Signatures_UnknownDocument_Is404WithTheApiBody()
    {
        var response = await _client.GetAsync($"/api/documents/{Guid.NewGuid()}/signatures");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString().Should().Be("Document not found.");
    }

    [Fact]
    public async Task Signatures_FailedDocument_Is409NotReady()
    {
        var id = await _host.UploadAcceptedAsync("broken.docx", Encoding.UTF8.GetBytes("not really a docx"));
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("failed");

        var response = await _client.GetAsync($"/api/documents/{id}/signatures");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ApiTestHost.ReadJsonAsync(response)).GetProperty("error").GetString()
            .Should().Be("The document is not ready. Current status: failed.");
    }

    [Fact]
    public async Task Signatures_NeverCallsAModel()
    {
        using var docx = SignedOfficeFixture.Docx("Contract", FakeOfficeSignature.By("Mario Rossi Prova"));

        await SignaturesOfAsync("signed.docx", docx.ToArray());

        _local.VerifyNoOtherCalls();
        _cloud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Signatures_UnsignedPdf_HasNoSignature()
    {
        (await SignaturesOfAsync("plain.pdf", SampleDocuments.Pdf("An ordinary document."))).GetProperty("signatures")
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Signatures_UnsignedWord_HasNoSignature()
    {
        (await SignaturesOfAsync("plain.docx", SampleDocuments.Docx("An ordinary document."))).GetProperty("signatures")
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Signatures_UnsignedExcel_HasNoSignature()
    {
        (await SignaturesOfAsync("plain.xlsx", SampleDocuments.Xlsx("An ordinary sheet."))).GetProperty("signatures")
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Signatures_TextFileThatQuotesTheWording_IsNotASignature()
    {
        var text = "file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00\n\nOnly a text file quoting the wording.";

        var body = await SignaturesOfAsync("quote.txt", SampleDocuments.Txt(text));

        body.GetProperty("signatures").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Signatures_PdfWhoseTextQuotesTheWordingFurtherDown_IsNotASignature()
    {
        var pdf = SampleDocuments.Pdf("An ordinary page. file firmato con firma digitale da Mario Rossi");

        var body = await SignaturesOfAsync("quote.pdf", pdf);

        body.GetProperty("signatures").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Signatures_DocumentWhoseStoredTextHasNoLines_IsReadAsHavingNoSignature()
    {
        // What a Word file uploaded before the signature lines were written looks like: its text has no line at its start.
        using var docx = SignedOfficeFixture.Docx("Text of a Word file uploaded before signatures were read.");

        var body = await SignaturesOfAsync("old.docx", docx.ToArray());

        body.GetProperty("signatures").GetArrayLength().Should().Be(0);
    }

    private async Task<System.Text.Json.JsonElement> SignaturesOfAsync(string fileName, byte[] bytes)
    {
        var id = await _host.UploadAcceptedAsync(fileName, bytes);
        (await _host.WaitForTerminalStatusAsync(id)).GetProperty("status").GetString().Should().Be("ready");

        var response = await _client.GetAsync($"/api/documents/{id}/signatures");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ApiTestHost.ReadJsonAsync(response);
    }

    private static IEnumerable<(string? Signer, string? SignedAt)> Entries(System.Text.Json.JsonElement body) =>
        body.GetProperty("signatures").EnumerateArray().Select(entry => (
            entry.GetProperty("signer").GetString(),
            entry.GetProperty("signedAt").GetString()));
}
