using System.Text;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class PdfDocumentParser : IDocumentParser
{
    public string SupportedFileType => "pdf";

    public Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)
    {
        return Task.FromResult(Extract(content, ct));
    }

    private static Result<string> Extract(Stream content, CancellationToken ct)
    {
        try
        {
            using var pdf = PdfDocument.Open(content);
            var text = ReadPages(pdf, ct);

            return string.IsNullOrWhiteSpace(text)
                ? Result<string>.Failure("No extractable text — scanned/image-only PDF not supported")
                : Result<string>.Success(text);
        }
        catch (PdfDocumentEncryptedException)
        {
            return Result<string>.Failure("Password-protected or encrypted PDF");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<string>.Failure("Corrupted or unreadable PDF");
        }
    }

    private static string ReadPages(PdfDocument pdf, CancellationToken ct)
    {
        var builder = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            builder.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        return builder.ToString().Trim();
    }
}
