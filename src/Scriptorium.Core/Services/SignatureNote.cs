using System.Globalization;
using System.Text.RegularExpressions;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// The one definition of the line that records a digital signature at the start of a document's extracted text:
/// it is written by the readers of PDF, Word and Excel files and read back by the signature buttons.
/// <c>file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00</c>
/// </summary>
public static partial class SignatureNote
{
    public const string Wording = "file firmato con firma digitale";

    private const string UnknownSigner = "(firmatario sconosciuto)";
    private const string KnownSignerPrefix = " da ";
    private const string DateSeparator = " — ";

    private static readonly string[] SignableTypes = ["pdf", "docx", "xlsx"];

    /// <summary>Builds the line. A null signer is the unknown signer; a null date leaves the date out.</summary>
    public static string Line(string? signer, string? signedAt)
    {
        var line = signer is null ? $"{Wording} {UnknownSigner}" : $"{Wording}{KnownSignerPrefix}{signer}";
        return signedAt is null ? line : $"{line}{DateSeparator}{signedAt}";
    }

    /// <summary>The date as the note shows it; the offset only when the file declared a zone. Never converted.</summary>
    public static string FormatDate(DateTimeOffset date, bool showZone)
    {
        var text = date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return showZone ? $"{text} {date.ToString("zzz", CultureInfo.InvariantCulture)}" : text;
    }

    /// <summary>
    /// The signatures recorded at the start of a text: the lines from the first line that are in the note format, ended by
    /// a blank line or by the end of the text. Anything else (the wording further down, a line that does not follow the
    /// layout) means there is no signature. Only the start counts.
    /// </summary>
    public static IReadOnlyList<SignatureEntry> ReadLeading(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var block = lines.TakeWhile(line => line.StartsWith(Wording, StringComparison.Ordinal)).ToList();
        if (block.Count == 0 || !EndsAtBlankLineOrEnd(lines, block.Count))
        {
            return [];
        }

        var entries = block.Select(Parse).ToList();
        return entries.Contains(null) ? [] : entries.OfType<SignatureEntry>().ToList();
    }

    public static bool CanBeSigned(string fileType) =>
        SignableTypes.Contains(fileType, StringComparer.OrdinalIgnoreCase);

    private static bool EndsAtBlankLineOrEnd(List<string> lines, int blockLength) =>
        blockLength == lines.Count || lines[blockLength].Length == 0;

    private static SignatureEntry? Parse(string line)
    {
        var rest = line[Wording.Length..];
        var (body, date) = SplitDate(rest);
        if (body == $" {UnknownSigner}")
        {
            return new SignatureEntry(null, date);
        }

        return body.StartsWith(KnownSignerPrefix, StringComparison.Ordinal) && body.Length > KnownSignerPrefix.Length
            ? new SignatureEntry(body[KnownSignerPrefix.Length..], date)
            : null;
    }

    private static (string Body, string? Date) SplitDate(string rest)
    {
        var match = DateAtTheEnd().Match(rest);
        return match.Success ? (rest[..match.Index], match.Groups["date"].Value) : (rest, null);
    }

    [GeneratedRegex(@" — (?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}( [+-]\d{2}:\d{2})?)$")]
    private static partial Regex DateAtTheEnd();
}
