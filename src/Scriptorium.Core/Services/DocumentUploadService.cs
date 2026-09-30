using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

public sealed class DocumentUploadService
{
    private readonly IFileStore _fileStore;
    private readonly IDocumentRepository _repository;
    private readonly IDocumentProcessingQueue _queue;
    private readonly TimeProvider _timeProvider;

    public DocumentUploadService(
        IFileStore fileStore,
        IDocumentRepository repository,
        IDocumentProcessingQueue queue,
        TimeProvider timeProvider)
    {
        _fileStore = fileStore;
        _repository = repository;
        _queue = queue;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Document>> UploadAsync(
        Stream content, string fileName, long fileSizeBytes, bool isPrivate, CancellationToken ct)
    {
        var id = Guid.NewGuid();

        var saved = await _fileStore.SaveAsync(id, fileName, content, ct);
        if (!saved.IsSuccess)
        {
            return Result<Document>.Failure(saved.Error);
        }

        var document = CreateDocument(id, fileName, fileSizeBytes, isPrivate, saved.Value);
        var added = await _repository.AddAsync(document, ct);
        if (!added.IsSuccess)
        {
            return Result<Document>.Failure(added.Error);
        }

        return await EnqueueAsync(document, ct);
    }

    private async Task<Result<Document>> EnqueueAsync(Document document, CancellationToken ct)
    {
        var queued = await _queue.EnqueueAsync(document.Id, ct);
        if (queued.IsSuccess)
        {
            return Result<Document>.Success(document);
        }

        await _repository.UpdateStatusAsync(document.Id, DocumentStatus.Failed, queued.Error, ct);
        return Result<Document>.Failure(queued.Error);
    }

    private Document CreateDocument(Guid id, string fileName, long sizeBytes, bool isPrivate, string storagePath)
    {
        return new Document
        {
            Id = id,
            FileName = fileName,
            FileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            FileSizeBytes = sizeBytes,
            StoragePath = storagePath,
            UploadDate = _timeProvider.GetUtcNow(),
            IsPrivate = isPrivate,
            Status = DocumentStatus.Processing
        };
    }
}
