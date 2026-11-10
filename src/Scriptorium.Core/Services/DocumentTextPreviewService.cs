using Scriptorium.Core.Ai;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// The use case "preview the extracted text of a document": the text, cut at the same limit the model reads
/// when a question is asked about the document.
/// </summary>
public sealed class DocumentTextPreviewService
{
    private readonly IDocumentRepository _repository;
    private readonly QaOptions _options;

    public DocumentTextPreviewService(IDocumentRepository repository, QaOptions options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<TextPreviewResult> GetPreviewAsync(Guid documentId, CancellationToken ct)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            return TextPreviewResult.Failure(TextPreviewFailureKind.NotFound, "Document not found.");
        }

        if (document.Status != DocumentStatus.Ready)
        {
            return TextPreviewResult.Failure(TextPreviewFailureKind.NotReady, NotReadyMessage(document));
        }

        var text = await _repository.GetExtractedTextAsync(documentId, ct);
        return text is null
            ? TextPreviewResult.Failure(TextPreviewFailureKind.NotReady, "The extracted text of this document is not available.")
            : ToSuccess(text);
    }

    private static string NotReadyMessage(Document document) =>
        $"Document is not ready. Current status: {document.Status.ToString().ToLowerInvariant()}.";

    private TextPreviewResult ToSuccess(ExtractedText text)
    {
        var excerpt = TextExcerpt.Of(text.Content, _options.MaxDocumentCharacters);
        return TextPreviewResult.Success(excerpt.Text, excerpt.Truncated, excerpt.OriginalLength);
    }
}
