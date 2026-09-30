using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class WordDocumentParserTests
{
    private readonly WordDocumentParser _parser = new();

    [Fact]
    public void SupportedFileType_IsDocx()
    {
        _parser.SupportedFileType.Should().Be("docx");
    }

    [Fact]
    public async Task ExtractTextAsync_ValidDocx_ReturnsAllParagraphs()
    {
        using var stream = BuildDocx("First paragraph", "Second paragraph");

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("First paragraph").And.Contain("Second paragraph");
    }

    [Fact]
    public async Task ExtractTextAsync_CorruptedDocx_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not a zip package"));

        var result = await _parser.ExtractTextAsync(stream, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Corrupted");
    }

    private static MemoryStream BuildDocx(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p))))));
        }

        stream.Position = 0;
        return stream;
    }
}
