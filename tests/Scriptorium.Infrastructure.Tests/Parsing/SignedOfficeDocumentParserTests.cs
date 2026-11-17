using FluentAssertions;
using Moq;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class SignedOfficeDocumentParserTests
{
    private readonly Mock<IDocumentParser> _inner = new(MockBehavior.Strict);
    private readonly Mock<IOfficeSignatureNoteReader> _notes = new(MockBehavior.Strict);

    private SignedOfficeDocumentParser Parser() => new(_inner.Object, _notes.Object);

    private void InnerReturns(Result<string> result)
    {
        _inner.Setup(p => p.SupportedFileType).Returns("docx");
        _inner.Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
    }

    private void NotesAre(params string[] lines) =>
        _notes.Setup(n => n.Read(It.IsAny<Stream>(), "docx", It.IsAny<CancellationToken>())).Returns(lines);

    [Fact]
    public void SupportedFileType_IsTheInnerOne()
    {
        _inner.Setup(p => p.SupportedFileType).Returns("xlsx");

        Parser().SupportedFileType.Should().Be("xlsx");
    }

    [Fact]
    public async Task ExtractTextAsync_InnerFailure_IsReturnedAsItIsAndNothingIsRead()
    {
        InnerReturns(Result<string>.Failure("Corrupted or unreadable Word document"));

        var result = await Parser().ExtractTextAsync(new MemoryStream(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Corrupted or unreadable Word document");
        _notes.Verify(n => n.Read(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_NoLines_ReturnsTheInnerTextUnchanged()
    {
        InnerReturns(Result<string>.Success("Plain text.\nSecond line."));
        NotesAre();

        var result = await Parser().ExtractTextAsync(new MemoryStream(), CancellationToken.None);

        result.Value.Should().Be("Plain text.\nSecond line.");
    }

    [Fact]
    public async Task ExtractTextAsync_Lines_AreInFrontOfTheTextWithABlankLineBetween()
    {
        InnerReturns(Result<string>.Success("Plain text."));
        NotesAre("first line", "second line");

        var result = await Parser().ExtractTextAsync(new MemoryStream(), CancellationToken.None);

        result.Value.Should().Be("first line\nsecond line\n\nPlain text.");
    }

    [Fact]
    public async Task ExtractTextAsync_LinesAndNoText_IsOnlyTheLines()
    {
        InnerReturns(Result<string>.Success(string.Empty));
        NotesAre("first line");

        var result = await Parser().ExtractTextAsync(new MemoryStream(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("first line");
    }

    [Fact]
    public async Task ExtractTextAsync_NoLinesAndNoText_StaysEmpty()
    {
        InnerReturns(Result<string>.Success(string.Empty));
        NotesAre();

        var result = await Parser().ExtractTextAsync(new MemoryStream(), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractTextAsync_PutsTheStreamBackAtItsStartBeforeReadingTheSignatures()
    {
        var content = new MemoryStream([1, 2, 3]);
        InnerReturns(Result<string>.Success("Plain text."));
        long? positionSeen = null;
        _notes.Setup(n => n.Read(It.IsAny<Stream>(), "docx", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>((stream, _, _) => positionSeen = stream.Position)
            .Returns([]);
        content.Position = 3;

        await Parser().ExtractTextAsync(content, CancellationToken.None);

        positionSeen.Should().Be(0);
    }

    [Fact]
    public async Task ExtractTextAsync_StreamThatCannotBePutBack_GivesTheInnerTextUnchangedAndNeverReads()
    {
        InnerReturns(Result<string>.Success("Plain text."));

        var result = await Parser().ExtractTextAsync(new ForwardOnlyStream(), CancellationToken.None);

        result.Value.Should().Be("Plain text.");
        _notes.Verify(n => n.Read(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_RealWordParserOnASignedFile_StartsWithTheLine()
    {
        var parser = new SignedOfficeDocumentParser(new WordDocumentParser(), new OfficeSignatureNoteReader());
        using var stream = SignedOfficeFixture.Docx("Contract text", FakeOfficeSignature.By("Mario Rossi Prova", "2026-03-14T10:32:00+01:00"));

        var result = await parser.ExtractTextAsync(stream, CancellationToken.None);

        var expected = SignatureNote.Line("Mario Rossi Prova", "2026-03-14 10:32:00 +01:00") + "\n\n" + "Contract text";
        result.Value.Should().Be(expected);
        SignatureNote.ReadLeading(result.Value ?? string.Empty).Should().Equal(new SignatureEntry("Mario Rossi Prova", "2026-03-14 10:32:00 +01:00"));
    }

    [Fact]
    public async Task ExtractTextAsync_RealExcelParserOnTwoSignatures_StartsWithTwoLinesInOrder()
    {
        var parser = new SignedOfficeDocumentParser(new ExcelDocumentParser(), new OfficeSignatureNoteReader());
        using var stream = SignedOfficeFixture.Xlsx(
            "Cell text",
            FakeOfficeSignature.By("Anna Verdi Prova", "2026-03-14T09:00:00Z"),
            FakeOfficeSignature.By("Luca Bianchi Prova"));

        var result = await parser.ExtractTextAsync(stream, CancellationToken.None);

        SignatureNote.ReadLeading(result.Value ?? string.Empty).Should().Equal(
            new SignatureEntry("Anna Verdi Prova", "2026-03-14 09:00:00 +00:00"),
            new SignatureEntry("Luca Bianchi Prova", null));
        result.Value.Should().EndWith("\n\nCell text");
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    public async Task ExtractTextAsync_UnsignedFile_GivesExactlyTheTextOfThePlainParser(string fileType)
    {
        IDocumentParser plain = fileType == "docx" ? new WordDocumentParser() : new ExcelDocumentParser();
        var decorated = new SignedOfficeDocumentParser(plain, new OfficeSignatureNoteReader());
        using var forPlain = SignedOfficeFixture.Build(fileType, "Some text");
        using var forDecorated = SignedOfficeFixture.Build(fileType, "Some text");

        var expected = await plain.ExtractTextAsync(forPlain, CancellationToken.None);
        var actual = await decorated.ExtractTextAsync(forDecorated, CancellationToken.None);

        actual.Value.Should().Be(expected.Value);
    }

    [Fact]
    public async Task ExtractTextAsync_SignedWordWithNoBodyText_IsASuccessWithOnlyTheLine()
    {
        var parser = new SignedOfficeDocumentParser(new WordDocumentParser(), new OfficeSignatureNoteReader());
        using var stream = SignedOfficeFixture.Docx(null, FakeOfficeSignature.By("Paolo Gialli Prova", "2026-06-10T12:00:00+02:00"));

        var result = await parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(SignatureNote.Line("Paolo Gialli Prova", "2026-06-10 12:00:00 +02:00"));
    }

    // A stream that can be read once and never rewound.
    private sealed class ForwardOnlyStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
