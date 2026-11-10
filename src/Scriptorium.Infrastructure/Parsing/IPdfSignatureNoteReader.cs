using UglyToad.PdfPig;

namespace Scriptorium.Infrastructure.Parsing;

public interface IPdfSignatureNoteReader
{
    /// <summary>
    /// One note per digital signature in the document, in the order of its signature fields.
    /// Empty when the document has no signature or when its signatures cannot be read; never throws.
    /// </summary>
    IReadOnlyList<string> Read(PdfDocument pdf);
}
