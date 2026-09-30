using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Interfaces;

public interface IDocumentRepository
{
    Task<Result> AddAsync(Document document, CancellationToken ct);

    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct);

    Task<Document?> FindByContentHashAsync(string contentHash, CancellationToken ct);

    /// <summary>Removes the document and, through the cascade, its extracted text.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);

    Task<Result> UpdateStatusAsync(Guid id, DocumentStatus status, string? failureReason, CancellationToken ct);

    Task<Result> SaveExtractedTextAsync(ExtractedText text, CancellationToken ct);

    Task<ExtractedText?> GetExtractedTextAsync(Guid documentId, CancellationToken ct);
}
