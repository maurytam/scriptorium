using System.Text;
using UglyToad.PdfPig.Tokens;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class SignerNameReader : ISignerNameReader
{
    private const int MaxLength = 100;

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
            return Clean(DeclaredName(signature)) ?? Clean(CertificateName(signature));
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

    // The name comes from the file and ends up in the text the model reads: keep it one short line of plain text.
    private static string? Clean(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var text = new StringBuilder();
        var pendingSpace = false;
        foreach (var character in name)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                pendingSpace = text.Length > 0;
                continue;
            }

            AppendWithPendingSpace(text, character, ref pendingSpace);
        }

        return Cut(text.ToString());
    }

    private static void AppendWithPendingSpace(StringBuilder text, char character, ref bool pendingSpace)
    {
        if (pendingSpace)
        {
            text.Append(' ');
            pendingSpace = false;
        }

        text.Append(character);
    }

    private static string? Cut(string text)
    {
        var cut = text.Length > MaxLength ? text[..MaxLength].TrimEnd() : text;
        if (cut.Length > 0 && char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return cut.Length == 0 ? null : cut;
    }
}
