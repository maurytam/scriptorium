using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// The use case "the digital signatures of a ready document". They are read from the whole stored text, where the upload
/// recorded them; no model is called and nothing is stored.
/// </summary>
public sealed class DocumentSignatureService
{
    private readonly IDocumentRepository _repository;

    public DocumentSignatureService(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<SignatureResult> ReadAsync(Guid documentId, CancellationToken ct)
    {
        var document = await ReadyDocumentText.LoadAsync(_repository, documentId, ct);
        if (!document.IsSuccess)
        {
            return SignatureResult.Failure(document.FailureKind, document.Error);
        }

        return SignatureResult.Success(
            SignatureNote.CanBeSigned(document.FileType) ? SignatureNote.ReadLeading(document.Value) : []);
    }
}
