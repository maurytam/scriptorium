namespace Scriptorium.Infrastructure.Parsing;

/// <summary>Reads the digital signatures of a Word (<c>docx</c>) or Excel (<c>xlsx</c>) file as lines of the signature note.</summary>
public interface IOfficeSignatureNoteReader
{
    /// <summary>
    /// One line per signature, in the order of the file; nothing when there is none or the file cannot be read.
    /// Only cancellation is allowed to escape. Nothing is validated: the file's own claims are reported.
    /// </summary>
    IReadOnlyList<string> Read(Stream content, string fileType, CancellationToken ct);
}
