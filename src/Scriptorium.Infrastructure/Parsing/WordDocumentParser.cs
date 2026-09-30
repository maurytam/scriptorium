using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class WordDocumentParser : IDocumentParser
{
    public string SupportedFileType => "docx";

    public Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)
    {
        return Task.FromResult(Extract(content, ct));
    }

    private static Result<string> Extract(Stream content, CancellationToken ct)
    {
        try
        {
            using var document = WordprocessingDocument.Open(content, false);
            var paragraphs = document.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>()
                ?? Enumerable.Empty<Paragraph>();

            var lines = paragraphs.Select(p => { ct.ThrowIfCancellationRequested(); return p.InnerText; });
            return Result<string>.Success(string.Join(Environment.NewLine, lines).Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<string>.Failure("Corrupted or unreadable Word document");
        }
    }
}
