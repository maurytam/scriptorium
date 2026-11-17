using FluentAssertions;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

/// <summary>What the PDF reader writes at the start of the text is read back, unchanged, by <see cref="SignatureNote"/>.</summary>
public class SignatureNoteRoundTripTests
{
    private readonly PdfDocumentParser _parser =
        new(new PdfSignatureNoteReader(new SignerNameReader(new CmsSignerCertificateReader())));

    [Theory]
    [InlineData("D:20260314103200+01'00'", "Mario Rossi", "2026-03-14 10:32:00 +01:00")]
    [InlineData("D:20260314103200Z", "Mario Rossi", "2026-03-14 10:32:00 +00:00")]
    [InlineData("D:20260314103200-05'00'", "Mario Rossi", "2026-03-14 10:32:00 -05:00")]
    [InlineData("D:20260314103200", "Mario Rossi", "2026-03-14 10:32:00")]
    [InlineData(null, "Mario Rossi", null)]
    [InlineData("D:20260314103200+01'00'", null, "2026-03-14 10:32:00 +01:00")]
    [InlineData(null, null, null)]
    public async Task OneSignature_IsReadBackAsWritten(string? signingDate, string? signerName, string? expectedDate)
    {
        var text = await Extract(SignedPdfFixture.Build("Hello", new FakeSignature(signingDate, SignerName: signerName)));

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry(signerName, expectedDate));
    }

    [Fact]
    public async Task TwoSignatures_AreReadBackInTheOrderOfTheFile()
    {
        var pdf = SignedPdfFixture.Build(
            "Hello",
            new FakeSignature("D:20260314103200+01'00'", SignerName: "Anna Verdi"),
            new FakeSignature(null, SignerName: "Luca Bianchi"));

        var text = await Extract(pdf);

        SignatureNote.ReadLeading(text).Should().Equal(
            new SignatureEntry("Anna Verdi", "2026-03-14 10:32:00 +01:00"),
            new SignatureEntry("Luca Bianchi", null));
    }

    [Fact]
    public async Task AnUnsignedPdf_IsReadAsHavingNoSignature()
    {
        var text = await Extract(SignedPdfFixture.Build("Hello"));

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Fact]
    public async Task ASignaturePlaceholderWithNoValue_IsReadAsHavingNoSignature()
    {
        var text = await Extract(SignedPdfFixture.Build("Hello", new FakeSignature(IsSigned: false)));

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    private async Task<string> Extract(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        return result.Value ?? string.Empty;
    }
}
