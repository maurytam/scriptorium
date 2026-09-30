using Microsoft.EntityFrameworkCore;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Persistence;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly IDbContextFactory<ScriptoriumDbContext> _contextFactory;

    public DocumentRepository(IDbContextFactory<ScriptoriumDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<Result> AddAsync(Document document, CancellationToken ct)
    {
        return WriteAsync("Unable to save the document", ct, async db =>
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        });
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public Task<Result> UpdateStatusAsync(
        Guid id, DocumentStatus status, string? failureReason, CancellationToken ct)
    {
        return WriteAsync("Unable to update the document status", ct, async db =>
        {
            var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (document is null)
            {
                return Result.Failure($"Document {id} not found");
            }

            document.Status = status;
            document.FailureReason = status == DocumentStatus.Failed ? failureReason : null;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        });
    }

    public Task<Result> SaveExtractedTextAsync(ExtractedText text, CancellationToken ct)
    {
        return WriteAsync("Unable to save the extracted text", ct, async db =>
        {
            db.ExtractedTexts.Add(text);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        });
    }

    public async Task<ExtractedText?> GetExtractedTextAsync(Guid documentId, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.ExtractedTexts.AsNoTracking().FirstOrDefaultAsync(t => t.DocumentId == documentId, ct);
    }

    private async Task<Result> WriteAsync(
        string failureMessage, CancellationToken ct, Func<ScriptoriumDbContext, Task<Result>> write)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync(ct);
            return await write(db);
        }
        catch (DbUpdateException)
        {
            return Result.Failure(failureMessage);
        }
    }
}
