using System.Text;
using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class PdfDocumentParserTests
{
    private readonly PdfDocumentParser _parser = new();

    [Fact]
    public void SupportedFileType_IsPdf()
    {
        _parser.SupportedFileType.Should().Be("pdf");
    }

    [Fact]
    public async Task ExtractTextAsync_ValidPdf_ReturnsPageText()
    {
        using var stream = new MemoryStream(BuildPdf("Hello Scriptorium"));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Hello").And.Contain("Scriptorium");
    }

    [Fact]
    public async Task ExtractTextAsync_CorruptedPdf_ReturnsCorruptedFailure()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("this is definitely not a pdf"));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Corrupted");
    }

    [Fact]
    public async Task ExtractTextAsync_EncryptedPdf_ReturnsEncryptedFailure()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(EncryptedPdf));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("encrypted");
    }

    [Fact]
    public async Task ExtractTextAsync_ImageOnlyPdf_ReturnsNoTextFailure()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        using var stream = new MemoryStream(builder.Build());

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No extractable text");
    }

    private static byte[] BuildPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    // Minimal PDF with a Standard security handler; the placeholder O/U entries never match
    // the empty user password, so opening it must fail as "encrypted".
    private static readonly string EncryptedPdf =
        "%PDF-1.4\n" +
        "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
        "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
        "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>\nendobj\n" +
        "4 0 obj\n<< /Filter /Standard /V 1 /R 2 /P -4 /O <" + new string('0', 64) + "> /U <" + new string('0', 64) + "> >>\nendobj\n" +
        "trailer\n<< /Size 5 /Root 1 0 R /Encrypt 4 0 R /ID [<" + new string('0', 32) + "> <" + new string('0', 32) + ">] >>\n" +
        "startxref\n0\n%%EOF\n";
}
