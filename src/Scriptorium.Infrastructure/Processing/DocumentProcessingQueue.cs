using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Processing;

public sealed class DocumentProcessingQueue : BackgroundService, IDocumentProcessingQueue
{
    private const int Capacity = 100;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(Capacity);
    private readonly IDocumentRepository _repository;
    private readonly IFileStore _fileStore;
    private readonly Dictionary<string, IDocumentParser> _parsers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentProcessingQueue> _logger;

    public DocumentProcessingQueue(
        IDocumentRepository repository,
        IFileStore fileStore,
        IEnumerable<IDocumentParser> parsers,
        TimeProvider timeProvider,
        ILogger<DocumentProcessingQueue> logger)
    {
        _repository = repository;
        _fileStore = fileStore;
        _parsers = parsers.ToDictionary(p => p.SupportedFileType, StringComparer.OrdinalIgnoreCase);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ValueTask<Result> EnqueueAsync(Guid documentId, CancellationToken ct)
    {
        var result = _channel.Writer.TryWrite(documentId)
            ? Result.Success()
            : Result.Failure("The processing queue is full, please try again later");
        return ValueTask.FromResult(result);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var documentId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessSafelyAsync(documentId, stoppingToken);
        }
    }

    private async Task ProcessSafelyAsync(Guid documentId, CancellationToken ct)
    {
        try
        {
            await ProcessAsync(documentId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error processing document {DocumentId}", documentId);
            await MarkFailedAsync(documentId, "Unexpected error while processing the document", ct);
        }
    }

    private async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found, skipping", documentId);
            return;
        }

        var extracted = await ExtractAsync(document, ct);
        if (!extracted.IsSuccess)
        {
            await MarkFailedAsync(documentId, extracted.Error, ct);
            return;
        }

        await CompleteAsync(documentId, extracted.Value, ct);
    }

    private async Task<Result<string>> ExtractAsync(Document document, CancellationToken ct)
    {
        if (!_parsers.TryGetValue(document.FileType, out var parser))
        {
            return Result<string>.Failure($"Unsupported file type '{document.FileType}'");
        }

        var opened = _fileStore.OpenRead(document.StoragePath);
        if (!opened.IsSuccess)
        {
            return Result<string>.Failure(opened.Error);
        }

        await using var stream = opened.Value;
        return await parser.ExtractTextAsync(stream, ct);
    }

    private async Task CompleteAsync(Guid documentId, string content, CancellationToken ct)
    {
        var text = new ExtractedText
        {
            DocumentId = documentId,
            Content = content,
            ExtractedAt = _timeProvider.GetUtcNow()
        };

        var saved = await _repository.SaveExtractedTextAsync(text, ct);
        if (!saved.IsSuccess)
        {
            await MarkFailedAsync(documentId, saved.Error, ct);
            return;
        }

        var ready = await _repository.UpdateStatusAsync(documentId, DocumentStatus.Ready, null, ct);
        if (!ready.IsSuccess)
        {
            _logger.LogError("Could not mark document {DocumentId} as ready: {Error}", documentId, ready.Error);
        }
    }

    private async Task MarkFailedAsync(Guid documentId, string reason, CancellationToken ct)
    {
        var updated = await _repository.UpdateStatusAsync(documentId, DocumentStatus.Failed, reason, ct);
        if (!updated.IsSuccess)
        {
            _logger.LogError("Could not mark document {DocumentId} as failed: {Error}", documentId, updated.Error);
        }
    }
}
