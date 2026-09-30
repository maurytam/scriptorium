using Scriptorium.Core.Results;

namespace Scriptorium.Core.Interfaces;

public interface IDocumentProcessingQueue
{
    ValueTask<Result> EnqueueAsync(Guid documentId, CancellationToken ct);
}
