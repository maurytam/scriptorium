using Scriptorium.Core.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Util;

namespace Scriptorium.Infrastructure.Parsing;

public sealed class PdfSignatureNoteReader : IPdfSignatureNoteReader
{
    private static readonly NameToken ValueKey = NameToken.Create("V");
    private static readonly NameToken SigningTimeKey = NameToken.Create("M");

    private readonly ISignerNameReader _signerNames;

    public PdfSignatureNoteReader(ISignerNameReader signerNames)
    {
        _signerNames = signerNames;
    }

    public IReadOnlyList<string> Read(PdfDocument pdf)
    {
        try
        {
            return ReadNotes(pdf);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private IReadOnlyList<string> ReadNotes(PdfDocument pdf)
    {
        if (!pdf.TryGetForm(out var form))
        {
            return [];
        }

        return form.GetFields()
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

    private string Note(DictionaryToken signature)
    {
        return SignatureNote.Line(_signerNames.Read(signature), SigningDate(signature));
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

        return raw is not null && HasDateHourAndMinute(raw) && DateFormatHelper.TryParseDateTimeOffset(raw, out var date)
            ? SignatureNote.FormatDate(date, DeclaresZone(raw))
            : null;
    }

    // A shorter date such as D:2026 would print as a false precision (1 January 00:00), so it counts as no date.
    private static bool HasDateHourAndMinute(string raw)
    {
        var body = raw.StartsWith("D:", StringComparison.Ordinal) ? raw[2..] : raw;
        return body.TakeWhile(char.IsAsciiDigit).Count() >= 12;
    }

    // PdfPig reports a date with no zone as +00:00, the same as "Z"; only show the offset when the file declares one.
    private static bool DeclaresZone(string raw)
    {
        var body = raw.StartsWith("D:", StringComparison.Ordinal) ? raw[2..] : raw;
        var zone = body.SkipWhile(char.IsAsciiDigit).FirstOrDefault();
        return zone is 'Z' or '+' or '-';
    }
}
