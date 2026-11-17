using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Infrastructure.Parsing;

/// <summary>
/// Puts one line per digital signature in front of the text a Word or Excel parser produced, as the PDF parser does for
/// PDFs. The wrapped parser is not changed; an unsigned file gives exactly its text.
/// </summary>
public sealed class SignedOfficeDocumentParser : IDocumentParser
{
    private readonly IDocumentParser _inner;
    private readonly IOfficeSignatureNoteReader _notes;

    public SignedOfficeDocumentParser(IDocumentParser inner, IOfficeSignatureNoteReader notes)
    {
        _inner = inner;
        _notes = notes;
    }

    public string SupportedFileType => _inner.SupportedFileType;

    public async Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)
    {
        var result = await _inner.ExtractTextAsync(content, ct);
        if (!result.IsSuccess || !content.CanSeek)
        {
            return result;
        }

        content.Position = 0;
        var lines = _notes.Read(content, SupportedFileType, ct);
        return lines.Count == 0 ? result : Result<string>.Success(WithLines(lines, result.Value));
    }

    // The lines come first, so they stay inside the model's document budget when a long text is cut at the end.
    private static string WithLines(IReadOnlyList<string> lines, string text) =>
        text.Length == 0 ? string.Join("\n", lines) : string.Join("\n", lines) + "\n\n" + text;
}
