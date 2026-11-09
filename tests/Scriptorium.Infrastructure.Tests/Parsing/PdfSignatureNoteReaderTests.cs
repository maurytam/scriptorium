using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class PdfSignatureNoteReaderTests
{
    private const string Wording = "file firmato con firma digitale";

    private readonly PdfSignatureNoteReader _reader = new();

    [Theory]
    [InlineData("D:20260314103200+01'00'", " — 2026-03-14 10:32:00 +01:00")]
    [InlineData("D:20260314103200Z", " — 2026-03-14 10:32:00 +00:00")]
    [InlineData("D:20260314103200-05'00'", " — 2026-03-14 10:32:00 -05:00")]
    [InlineData("D:20260314103200", " — 2026-03-14 10:32:00")]
    public void Read_SignatureWithDate_ReturnsNoteWithDateAndDeclaredZone(string signingDate, string expectedSuffix)
    {
        var notes = ReadNotes(SignedPdfFixture.Build("Hello", new FakeSignature(signingDate)));

        notes.Should().Equal(Wording + expectedSuffix);
    }

    [Fact]
    public void Read_PdfWithoutForm_ReturnsNoNotes()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Hello", 12, new PdfPoint(25, 700), font);

        ReadNotes(builder.Build()).Should().BeEmpty();
    }

    [Fact]
    public void Read_FormWithoutSignature_ReturnsNoNotes()
    {
        ReadNotes(SignedPdfFixture.Build("Hello")).Should().BeEmpty();
    }

    private IReadOnlyList<string> ReadNotes(byte[] pdfBytes)
    {
        using var pdf = PdfDocument.Open(pdfBytes);
        return _reader.Read(pdf);
    }
}
