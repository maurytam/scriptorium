using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

public sealed class DocumentDeletionService
{
    private readonly IDocumentRepository _repository;
    private readonly IFileStore _fileStore;

    public DocumentDeletionService(IDocumentRepository repository, IFileStore fileStore)
    {
        _repository = repository;
        _fileStore = fileStore;
    }

    public async Task<DeleteResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await _repository.GetByIdAsync(id, ct);
        if (document is null)
        {
            return DeleteResult.Failure(DeleteFailureKind.NotFound, "Document not found.");
        }

        if (document.Status == DocumentStatus.Processing)
        {
            return DeleteResult.Failure(
                DeleteFailureKind.StillProcessing,
                "The document is still being processed and cannot be deleted yet.");
        }

        // Database first: a leftover file is harmless, a listed document without its file is not.
        var removed = await _repository.DeleteAsync(id, ct);
        if (!removed.IsSuccess)
        {
            return DeleteResult.Failure(DeleteFailureKind.Internal, removed.Error);
        }

        var files = _fileStore.Delete(id);
        return files.IsSuccess
            ? DeleteResult.Success()
            : DeleteResult.Failure(DeleteFailureKind.Internal, files.Error);
    }
}
