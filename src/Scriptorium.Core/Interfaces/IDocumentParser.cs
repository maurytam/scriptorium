using Scriptorium.Core.Results;

namespace Scriptorium.Core.Interfaces;

public interface IDocumentParser
{
    string SupportedFileType { get; }

    Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct);
}
