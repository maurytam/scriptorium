using FluentAssertions;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class SignatureNoteTests
{
    private const string Wording = "file firmato con firma digitale";

    [Theory]
    [InlineData("Mario Rossi", "2026-03-14 10:32:00 +01:00", "file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00")]
    [InlineData("Mario Rossi", "2026-03-14 10:32:00", "file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00")]
    [InlineData("Mario Rossi", null, "file firmato con firma digitale da Mario Rossi")]
    [InlineData(null, "2026-03-14 10:32:00 +01:00", "file firmato con firma digitale (firmatario sconosciuto) — 2026-03-14 10:32:00 +01:00")]
    [InlineData(null, null, "file firmato con firma digitale (firmatario sconosciuto)")]
    public void Line_HasTheLayoutOfTheDocumentedNote(string? signer, string? signedAt, string expected)
    {
        SignatureNote.Line(signer, signedAt).Should().Be(expected);
    }

    [Fact]
    public void FormatDate_WithZone_ShowsTheOffset()
    {
        var date = new DateTimeOffset(2026, 3, 14, 10, 32, 0, TimeSpan.FromHours(1));

        SignatureNote.FormatDate(date, showZone: true).Should().Be("2026-03-14 10:32:00 +01:00");
    }

    [Fact]
    public void FormatDate_WithoutZone_ShowsOnlyDateAndTime()
    {
        var date = new DateTimeOffset(2026, 3, 14, 10, 32, 0, TimeSpan.FromHours(1));

        SignatureNote.FormatDate(date, showZone: false).Should().Be("2026-03-14 10:32:00");
    }

    [Fact]
    public void FormatDate_DoesNotConvertToTheLocalTimeZone()
    {
        var date = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(-5));

        SignatureNote.FormatDate(date, showZone: true).Should().Be("2026-12-31 23:59:59 -05:00");
    }

    [Fact]
    public void ReadLeading_OneLineFollowedByBlankLineAndText_ReadsTheSigner()
    {
        var text = $"{Wording} da Mario Rossi — 2026-03-14 10:32:00 +01:00\n\nPage text.";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry("Mario Rossi", "2026-03-14 10:32:00 +01:00"));
    }

    [Fact]
    public void ReadLeading_SeveralLines_KeepsTheOrderOfTheFile()
    {
        var text = $"{Wording} da Anna Verdi — 2026-03-14 09:00:00\n{Wording} da Luca Bianchi\n{Wording} (firmatario sconosciuto)\n\nText.";

        SignatureNote.ReadLeading(text).Should().Equal(
            new SignatureEntry("Anna Verdi", "2026-03-14 09:00:00"),
            new SignatureEntry("Luca Bianchi", null),
            new SignatureEntry(null, null));
    }

    [Fact]
    public void ReadLeading_LinesThatAreTheWholeText_AreRead()
    {
        var text = $"{Wording} da Paolo Gialli — 2026-06-10 12:00:00 +02:00";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry("Paolo Gialli", "2026-06-10 12:00:00 +02:00"));
    }

    [Fact]
    public void ReadLeading_WindowsLineEnds_AreRead()
    {
        var text = $"{Wording} da Anna Verdi — 2026-03-14 09:00:00\r\n{Wording} da Luca Bianchi\r\n\r\nText.";

        SignatureNote.ReadLeading(text).Select(e => e.Signer).Should().Equal("Anna Verdi", "Luca Bianchi");
    }

    [Fact]
    public void ReadLeading_UnknownSignerWithADate_HasNoSignerAndTheDate()
    {
        var text = $"{Wording} (firmatario sconosciuto) — 2026-03-14 10:32:00 +01:00\n\nText.";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry(null, "2026-03-14 10:32:00 +01:00"));
    }

    [Fact]
    public void ReadLeading_NameContainingTheLongDash_IsReadWhole()
    {
        var text = $"{Wording} da Mario — Rossi — 2026-03-14 10:32:00\n\nText.";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry("Mario — Rossi", "2026-03-14 10:32:00"));
    }

    [Fact]
    public void ReadLeading_NameThatLooksLikeADate_IsReadWholeAndTheDateIsTheDeclaredOne()
    {
        var text = $"{Wording} da Mario 2026-01-01 00:00:00 Rossi — 2026-03-14 10:32:00\n\nText.";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry("Mario 2026-01-01 00:00:00 Rossi", "2026-03-14 10:32:00"));
    }

    [Fact]
    public void ReadLeading_WordingNotOnTheFirstLine_ReadsNothing()
    {
        var text = $"Some title\n{Wording} da Mario Rossi\n\nText.";

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Fact]
    public void ReadLeading_WordingFurtherDown_ReadsNothing()
    {
        var text = $"Page text.\n\n{Wording} da Mario Rossi — 2026-03-14 10:32:00";

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Fact]
    public void ReadLeading_LineNotFollowedByABlankLine_ReadsNothing()
    {
        var text = $"{Wording} da Mario Rossi\nThe text starts right here.";

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Fact]
    public void ReadLeading_LineThatDoesNotFollowTheLayout_RejectsTheWholeBlock()
    {
        var text = $"{Wording} da Anna Verdi\n{Wording} and then something else\n\nText.";

        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Only ordinary text.")]
    public void ReadLeading_TextWithNoNote_ReadsNothing(string text)
    {
        SignatureNote.ReadLeading(text).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Mario Rossi", "2026-03-14 10:32:00 +01:00")]
    [InlineData("Mario Rossi", "2026-03-14 10:32:00")]
    [InlineData("Mario Rossi", null)]
    [InlineData(null, "2026-03-14 10:32:00 -05:30")]
    [InlineData(null, null)]
    public void ReadLeading_OfALineBuiltByLine_ReturnsWhatWasWritten(string? signer, string? signedAt)
    {
        var text = SignatureNote.Line(signer, signedAt) + "\n\nText.";

        SignatureNote.ReadLeading(text).Should().Equal(new SignatureEntry(signer, signedAt));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("PDF")]
    [InlineData("Docx")]
    [InlineData("XLSX")]
    public void CanBeSigned_PdfWordAndExcel_IsTrue(string fileType)
    {
        SignatureNote.CanBeSigned(fileType).Should().BeTrue();
    }

    [Theory]
    [InlineData("txt")]
    [InlineData("doc")]
    [InlineData("")]
    public void CanBeSigned_AnyOtherType_IsFalse(string fileType)
    {
        SignatureNote.CanBeSigned(fileType).Should().BeFalse();
    }
}
