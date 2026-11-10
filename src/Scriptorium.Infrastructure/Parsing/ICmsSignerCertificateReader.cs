namespace Scriptorium.Infrastructure.Parsing;

public interface ICmsSignerCertificateReader
{
    /// <summary>
    /// The common name of the signer's certificate inside the <c>/Contents</c> bytes of a signature
    /// (a CMS signed-data structure, possibly followed by zero padding). Null when the structure cannot be read
    /// or when the signer cannot be identified with certainty: the answer is never guessed. Never throws.
    /// </summary>
    string? ReadSignerName(ReadOnlyMemory<byte> contents);
}
