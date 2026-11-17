using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Scriptorium.Core.Services;

namespace Scriptorium.Infrastructure.Parsing;

public sealed partial class OfficeSignatureNoteReader : IOfficeSignatureNoteReader
{
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public IReadOnlyList<string> Read(Stream content, string fileType, CancellationToken ct)
    {
        try
        {
            return ReadNotes(content, fileType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static List<string> ReadNotes(Stream content, string fileType, CancellationToken ct)
    {
        switch (fileType.ToLowerInvariant())
        {
            case "docx":
                using (var word = WordprocessingDocument.Open(content, false))
                {
                    return NotesOf(word.DigitalSignatureOriginPart, ct);
                }

            case "xlsx":
                using (var excel = SpreadsheetDocument.Open(content, false))
                {
                    return NotesOf(excel.DigitalSignatureOriginPart, ct);
                }

            default:
                return [];
        }
    }

    private static List<string> NotesOf(DigitalSignatureOriginPart? origin, CancellationToken ct)
    {
        var notes = new List<string>();
        foreach (var part in origin?.XmlSignatureParts ?? [])
        {
            ct.ThrowIfCancellationRequested();
            var note = NoteOf(part);
            if (note is not null)
            {
                notes.Add(note);
            }
        }

        return notes;
    }

    private static string? NoteOf(XmlSignaturePart part)
    {
        var xml = Load(part);
        return xml is null ? null : SignatureNote.Line(SignerName(xml), SigningDate(xml));
    }

    // The part comes from an upload that is not trusted: no DTD, no external resolver, a bounded size.
    private static XDocument? Load(XmlSignaturePart part)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 };
            using var reader = XmlReader.Create(part.GetStream(), settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string? SignerName(XDocument xml)
    {
        var certificates = LoadCertificates(xml);
        try
        {
            var signer = certificates.FirstOrDefault(c => !IsAuthority(c) && !IssuedAnother(c, certificates)) ?? certificates.FirstOrDefault();
            return SignerNameText.Clean(signer?.GetNameInfo(X509NameType.SimpleName, false));
        }
        finally
        {
            certificates.ForEach(certificate => certificate.Dispose());
        }
    }

    // A certificate that cannot be read must not stop the others from being read.
    private static List<X509Certificate2> LoadCertificates(XDocument xml)
    {
        var certificates = new List<X509Certificate2>();
        foreach (var element in xml.Descendants(Ds + "X509Certificate"))
        {
            try
            {
                certificates.Add(X509CertificateLoader.LoadCertificate(Convert.FromBase64String(element.Value.Trim())));
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
            }
        }

        return certificates;
    }

    private static bool IsAuthority(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(extension => extension.CertificateAuthority);

    private static bool IssuedAnother(X509Certificate2 certificate, List<X509Certificate2> all) =>
        all.Any(other => !ReferenceEquals(other, certificate)
                         && other.IssuerName.RawData.AsSpan().SequenceEqual(certificate.SubjectName.RawData)
                         && !other.SubjectName.RawData.AsSpan().SequenceEqual(certificate.SubjectName.RawData));

    // The time the signature declares: the Office one first, then the XAdES one that other programs write.
    private static string? SigningDate(XDocument xml)
    {
        var officeTime = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "SignatureTime")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value;
        var xadesTime = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "SigningTime")?.Value;
        return IsoDate(officeTime) ?? IsoDate(xadesTime);
    }

    // At least date, hour and minute: a shorter date would print as a false precision.
    private static string? IsoDate(string? raw)
    {
        var text = raw?.Trim();
        if (text is null || !DateHourAndMinute().IsMatch(text)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return null;
        }

        return SignatureNote.FormatDate(date, DeclaresZone().IsMatch(text));
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}")]
    private static partial Regex DateHourAndMinute();

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex DeclaresZone();
}
