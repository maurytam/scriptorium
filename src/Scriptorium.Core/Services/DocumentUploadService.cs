using System.Security.Cryptography;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

public sealed class DocumentUploadService
{
    private const long BytesPerMegabyte = 1024 * 1024;

    private readonly IFileStore _fileStore;
    private readonly IDocumentRepository _repository;
    private readonly IDocumentProcessingQueue _queue;
    private readonly IReadOnlyList<string> _supportedFileTypes;
    private readonly TimeProvider _timeProvider;
    private readonly long _maxSizeBytes;

    public DocumentUploadService(
        IFileStore fileStore,
        IDocumentRepository repository,
        IDocumentProcessingQueue queue,
        IEnumerable<IDocumentParser> parsers,
        TimeProvider timeProvider,
        long maxSizeBytes)
    {
        _fileStore = fileStore;
        _repository = repository;
        _queue = queue;
        _supportedFileTypes = parsers.Select(p => p.SupportedFileType).ToList();
        _timeProvider = timeProvider;
        _maxSizeBytes = maxSizeBytes;
    }

    public async Task<UploadResult> UploadAsync(
        Stream content, string fileName, long fileSizeBytes, bool isPrivate, CancellationToken ct)
    {
        var inspection = await InspectAsync(content, fileName, fileSizeBytes, ct);
        if (inspection.Rejection is not null)
        {
            return inspection.Rejection;
        }

        var id = Guid.NewGuid();
        var saved = await _fileStore.SaveAsync(id, fileName, content, ct);
        if (!saved.IsSuccess)
        {
            return UploadResult.Failure(UploadFailureKind.Internal, saved.Error);
        }

        var document = CreateDocument(id, fileName, fileSizeBytes, isPrivate, saved.Value, inspection.ContentHash);
        var added = await _repository.AddAsync(document, ct);
        if (!added.IsSuccess)
        {
            return UploadResult.Failure(UploadFailureKind.Internal, added.Error);
        }

        return await EnqueueAsync(document, ct);
    }

    /// <summary>Checks type, size and duplicates before anything is stored.</summary>
    private async Task<Inspection> InspectAsync(Stream content, string fileName, long fileSizeBytes, CancellationToken ct)
    {
        var invalid = Validate(fileName, fileSizeBytes);
        if (invalid is not null)
        {
            return new Inspection(string.Empty, invalid);
        }

        var hash = await ComputeHashAsync(content, ct);
        if (hash is null)
        {
            return new Inspection(string.Empty, UploadResult.Failure(UploadFailureKind.Internal, "Unable to read the uploaded file."));
        }

        var existing = await _repository.FindByContentHashAsync(hash, ct);
        return existing is null
            ? new Inspection(hash, null)
            : new Inspection(hash, UploadResult.Failure(
                UploadFailureKind.Duplicate, $"This file has already been uploaded as '{existing.FileName}'."));
    }

    /// <summary>Hashes the whole stream and rewinds it; returns null when the stream cannot be rewound.</summary>
    private static async Task<string?> ComputeHashAsync(Stream content, CancellationToken ct)
    {
        if (!content.CanSeek)
        {
            return null;
        }

        var start = content.Position;
        var hash = await SHA256.HashDataAsync(content, ct);
        content.Position = start;
        return Convert.ToHexString(hash);
    }

    private UploadResult? Validate(string fileName, long fileSizeBytes)
    {
        var extension = Path.GetExtension(fileName);
        if (!_supportedFileTypes.Contains(extension.TrimStart('.'), StringComparer.OrdinalIgnoreCase))
        {
            var shown = extension.Length > 0 ? extension : "(no extension)";
            return UploadResult.Failure(
                UploadFailureKind.UnsupportedType,
                $"Unsupported file type '{shown}'. Supported types: {string.Join(", ", _supportedFileTypes)}.");
        }

        return fileSizeBytes > _maxSizeBytes
            ? UploadResult.Failure(
                UploadFailureKind.FileTooLarge,
                $"File exceeds the maximum size of {_maxSizeBytes / BytesPerMegabyte} MB.")
            : null;
    }

    private async Task<UploadResult> EnqueueAsync(Document document, CancellationToken ct)
    {
        var queued = await _queue.EnqueueAsync(document.Id, ct);
        if (queued.IsSuccess)
        {
            return UploadResult.Success(document);
        }

        await _repository.UpdateStatusAsync(document.Id, DocumentStatus.Failed, queued.Error, ct);
        return UploadResult.Failure(UploadFailureKind.Internal, queued.Error);
    }

    private Document CreateDocument(
        Guid id, string fileName, long sizeBytes, bool isPrivate, string storagePath, string contentHash)
    {
        return new Document
        {
            Id = id,
            FileName = fileName,
            FileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            FileSizeBytes = sizeBytes,
            StoragePath = storagePath,
            ContentHash = contentHash,
            UploadDate = _timeProvider.GetUtcNow(),
            IsPrivate = isPrivate,
            Status = DocumentStatus.Processing
        };
    }

    private sealed record Inspection(string ContentHash, UploadResult? Rejection);
}
