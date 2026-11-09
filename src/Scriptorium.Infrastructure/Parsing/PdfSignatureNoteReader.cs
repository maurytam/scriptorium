using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Util;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class PdfSignatureNoteReader : IPdfSignatureNoteReader
{
    private const string Wording = "file firmato con firma digitale";

    private static readonly NameToken ValueKey = NameToken.Create("V");
    private static readonly NameToken SigningTimeKey = NameToken.Create("M");

    public IReadOnlyList<string> Read(PdfDocument pdf)
    {
        if (!pdf.TryGetForm(out var form))
        {
            return [];
        }

        return form.Fields
            .OfType<AcroSignatureField>()
            .Select(field => SignatureDictionary(pdf, field))
            .OfType<DictionaryToken>()
            .Select(Note)
            .ToList();
    }

    private static DictionaryToken? SignatureDictionary(PdfDocument pdf, AcroSignatureField field)
    {
        if (!field.Dictionary.TryGet(ValueKey, out var value))
        {
            return null;
        }

        var resolved = value is IndirectReferenceToken reference ? pdf.Structure.GetObject(reference.Data)?.Data : value;
        return resolved as DictionaryToken;
    }

    private static string Note(DictionaryToken signature)
    {
        var date = SigningDate(signature);
        return date is null ? Wording : $"{Wording} — {date}";
    }

    private static string? SigningDate(DictionaryToken signature)
    {
        if (!signature.TryGet(SigningTimeKey, out var token))
        {
            return null;
        }

        var raw = token switch
        {
            StringToken text => text.Data,
            HexToken hex => hex.Data,
            _ => null,
        };

        return raw is not null && DateFormatHelper.TryParseDateTimeOffset(raw, out var date) ? Format(date, raw) : null;
    }

    private static string Format(DateTimeOffset date, string raw)
    {
        var text = date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return DeclaresZone(raw) ? $"{text} {date.ToString("zzz", CultureInfo.InvariantCulture)}" : text;
    }

    // PdfPig reports a date with no zone as +00:00, the same as "Z"; only show the offset when the file declares one.
    private static bool DeclaresZone(string raw)
    {
        var body = raw.StartsWith("D:", StringComparison.Ordinal) ? raw[2..] : raw;
        var zone = body.SkipWhile(char.IsAsciiDigit).FirstOrDefault();
        return zone is 'Z' or '+' or '-';
    }
}
