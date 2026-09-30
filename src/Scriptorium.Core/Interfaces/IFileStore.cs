using Scriptorium.Core.Results;

namespace Scriptorium.Core.Interfaces;

public interface IFileStore
{
    Task<Result<string>> SaveAsync(Guid documentId, string fileName, Stream content, CancellationToken ct);

    Result<Stream> OpenRead(string storagePath);

    /// <summary>Deletes everything stored for the document; succeeds when nothing is stored.</summary>
    Result Delete(Guid documentId);
}
