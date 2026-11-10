using UglyToad.PdfPig.Tokens;

namespace Scriptorium.Infrastructure.Parsing;

public interface ISignerNameReader
{
    /// <summary>
    /// The name of whoever signed, as the signature declares it: its <c>/Name</c> entry when it is not blank,
    /// otherwise the name of the signer's certificate found in <c>/Contents</c>. The result is one clean line
    /// of at most 100 characters. Null when no name can be found; never guessed, never throws.
    /// </summary>
    string? Read(DictionaryToken signature);
}
