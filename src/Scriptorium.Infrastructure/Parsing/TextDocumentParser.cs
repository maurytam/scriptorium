using System.Text;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class TextDocumentParser : IDocumentParser
{
    public string SupportedFileType => "txt";

    public async Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = await reader.ReadToEndAsync(ct);
            return Result<string>.Success(text);
        }
        catch (IOException)
        {
            return Result<string>.Failure("Unable to read file");
        }
    }
}
