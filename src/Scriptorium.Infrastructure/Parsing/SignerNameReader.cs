using System.Text;
using UglyToad.PdfPig.Tokens;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class SignerNameReader : ISignerNameReader
{
    private static readonly NameToken NameKey = NameToken.Create("Name");
    private static readonly NameToken ContentsKey = NameToken.Create("Contents");

    private readonly ICmsSignerCertificateReader _certificates;

    public SignerNameReader(ICmsSignerCertificateReader certificates)
    {
        _certificates = certificates;
    }

    public string? Read(DictionaryToken signature)
    {
        try
        {
            return SignerNameText.Clean(DeclaredName(signature)) ?? SignerNameText.Clean(CertificateName(signature));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static string? DeclaredName(DictionaryToken signature)
    {
        if (!signature.TryGet(NameKey, out var token))
        {
            return null;
        }

        return token switch
        {
            StringToken text => text.Data,
            HexToken hex => Decode(hex.Bytes.ToArray()),
            _ => null,
        };
    }

    // A hexadecimal string starting with the byte order mark FE FF is UTF-16 big-endian; anything else is Latin-1.
    private static string Decode(byte[] bytes) =>
        bytes is [0xFE, 0xFF, ..]
            ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
            : Encoding.Latin1.GetString(bytes);

    private string? CertificateName(DictionaryToken signature) =>
        signature.TryGet(ContentsKey, out var token) && token is HexToken contents
            ? _certificates.ReadSignerName(contents.Bytes.ToArray())
            : null;
}
