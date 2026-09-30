using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Storage;

public sealed class LocalFileStore : IFileStore
{
    private readonly string _rootPath;

    public LocalFileStore(string rootPath)
    {
        _rootPath = rootPath;
    }

    public async Task<Result<string>> SaveAsync(
        Guid documentId, string fileName, Stream content, CancellationToken ct)
    {
        try
        {
            var safeName = Path.GetFileName(fileName);
            var directory = Path.Combine(_rootPath, documentId.ToString());
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, safeName);
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(target, ct);

            return Result<string>.Success(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Failure("Unable to store the uploaded file");
        }
    }

    public Result<Stream> OpenRead(string storagePath)
    {
        try
        {
            var stream = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Result<Stream>.Success(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<Stream>.Failure("Unable to read the stored file");
        }
    }
}
