using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

/// <summary>
/// One certificate listed in a fake signature. <see cref="CommonName"/> is the subject (an invented name).
/// <see cref="IsAuthority"/> makes it a certification authority; <see cref="IsGarbage"/> writes text that is not a certificate.
/// </summary>
public sealed record FakeOfficeCertificate(string CommonName, bool IsAuthority = false, bool IsGarbage = false);

/// <summary>
/// One fake digital signature of a Word or Excel file. <see cref="Certificates"/> are written in this order (none = no
/// certificate); <see cref="SigningTime"/> is written as-is as the Office <c>SignatureTime</c> and
/// <see cref="XadesSigningTime"/> as the XAdES <c>SigningTime</c> (null = not written). <see cref="BrokenXml"/> writes a part
/// that is not XML; <see cref="WithDoctype"/> adds a DOCTYPE with an entity.
/// </summary>
public sealed record FakeOfficeSignature(
    IReadOnlyList<FakeOfficeCertificate>? Certificates = null,
    string? SigningTime = null,
    string? XadesSigningTime = null,
    bool BrokenXml = false,
    bool WithDoctype = false)
{
    /// <summary>A signature by one signer, with an optional Office signing time.</summary>
    public static FakeOfficeSignature By(string signerName, string? signingTime = null, string? xadesSigningTime = null) =>
        new([new FakeOfficeCertificate(signerName)], signingTime, xadesSigningTime);
}

/// <summary>
/// Builds fake signed Word and Excel files in memory: the package has a signature origin part and one signature part per
/// <see cref="FakeOfficeSignature"/>, holding a hand-written XML-DSig body with self-made certificates of invented names.
/// The signature value is not a real signature and is never checked.
/// </summary>
public static class SignedOfficeFixture
{
    private const string Ds = "http://www.w3.org/2000/09/xmldsig#";
    private const string Md = "http://schemas.openxmlformats.org/package/2006/digital-signature";
    private const string Xd = "http://uri.etsi.org/01903/v1.3.2#";

    public static MemoryStream Docx(string? bodyText, params FakeOfficeSignature[] signatures)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var body = new Body();
            foreach (var line in (bodyText ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                body.Append(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(line))));
            }

            document.AddMainDocumentPart().Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
            AddSignatures(document, signatures);
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream Xlsx(string? cellText, params FakeOfficeSignature[] signatures)
    {
        var stream = new MemoryStream();
        using (var workbook = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = workbook.AddWorkbookPart();
            workbookPart.Workbook = new Workbook(new Sheets());
            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            if (!string.IsNullOrEmpty(cellText))
            {
                data.Append(new Row(new Cell
                {
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(cellText)),
                }) { RowIndex = 1 });
            }

            sheetPart.Worksheet = new Worksheet(data);
            workbookPart.Workbook.GetFirstChild<Sheets>()?.Append(
                new Sheet { Id = workbookPart.GetIdOfPart(sheetPart), SheetId = 1, Name = "Sheet1" });
            AddSignatures(workbook, signatures);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>The same file with the given type: <c>docx</c> or <c>xlsx</c>.</summary>
    public static MemoryStream Build(string fileType, string? text, params FakeOfficeSignature[] signatures) =>
        fileType == "docx" ? Docx(text, signatures) : Xlsx(text, signatures);

    private static void AddSignatures(OpenXmlPackage package, FakeOfficeSignature[] signatures)
    {
        if (signatures.Length == 0)
        {
            return;
        }

        var origin = package.AddNewPart<DigitalSignatureOriginPart>();
        foreach (var signature in signatures)
        {
            using var writer = new StreamWriter(origin.AddNewPart<XmlSignaturePart>().GetStream(FileMode.Create));
            writer.Write(Body(signature));
        }
    }

    private static string Body(FakeOfficeSignature signature)
    {
        if (signature.BrokenXml)
        {
            return "<Signature><this is not closed";
        }

        var doctype = signature.WithDoctype ? "<!DOCTYPE Signature [<!ENTITY secret \"x\">]>" : string.Empty;
        return doctype + $"<Signature xmlns=\"{Ds}\" Id=\"idPackageSignature\"><SignedInfo/><SignatureValue>AA==</SignatureValue>" +
               $"<KeyInfo><X509Data>{Certificates(signature.Certificates)}</X509Data></KeyInfo>" +
               $"<Object Id=\"idPackageObject\"><SignatureProperties>{OfficeTime(signature.SigningTime)}</SignatureProperties></Object>" +
               XadesTime(signature.XadesSigningTime) + "</Signature>";
    }

    private static string OfficeTime(string? time) =>
        time is null
            ? string.Empty
            : $"<SignatureProperty Id=\"idSignatureTime\" Target=\"#idPackageSignature\"><SignatureTime xmlns=\"{Md}\">" +
              $"<Format>YYYY-MM-DDThh:mm:ssTZD</Format><Value>{time}</Value></SignatureTime></SignatureProperty>";

    private static string XadesTime(string? time) =>
        time is null
            ? string.Empty
            : $"<Object><QualifyingProperties xmlns=\"{Xd}\" Target=\"#idPackageSignature\"><SignedProperties>" +
              $"<SignedSignatureProperties><SigningTime>{time}</SigningTime></SignedSignatureProperties></SignedProperties></QualifyingProperties></Object>";

    // Authorities are made first (the first one is self-signed, the others are issued by it); every other certificate is
    // issued by the last authority, or is self-signed when there is none. They are written in the order given. The keys of
    // the authorities are kept in hand, so they can sign without relying on a private key attached to a certificate.
    private static string Certificates(IReadOnlyList<FakeOfficeCertificate>? certificates)
    {
        if (certificates is null || certificates.Count == 0)
        {
            return string.Empty;
        }

        var made = new Dictionary<FakeOfficeCertificate, string>();
        var authorities = new List<Issuer>();
        foreach (var authority in certificates.Where(c => c.IsAuthority && !c.IsGarbage))
        {
            var made1 = Make(authority, authorities.Count == 0 ? null : authorities[0]);
            authorities.Add(made1);
            made[authority] = Convert.ToBase64String(made1.Certificate.RawData);
        }

        var signerIssuer = authorities.Count == 0 ? null : authorities[^1];
        foreach (var other in certificates.Where(c => !c.IsAuthority || c.IsGarbage))
        {
            made[other] = other.IsGarbage ? "bm90IGEgY2VydGlmaWNhdGU=" : Convert.ToBase64String(Make(other, signerIssuer).Certificate.RawData);
        }

        return string.Concat(certificates.Select(c => $"<X509Certificate>{made[c]}</X509Certificate>"));
    }

    private sealed record Issuer(X509Certificate2 Certificate, RSA Key);

    private static Issuer Make(FakeOfficeCertificate fake, Issuer? issuer)
    {
        var key = RSA.Create(2048);
        var subject = new X500DistinguishedName($"CN=\"{fake.CommonName.Replace("\"", "'")}\"");
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(fake.IsAuthority, false, 0, true));
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddYears(1);
        var certificate = issuer is null
            ? request.CreateSelfSigned(from, to)
            : request.Create(
                issuer.Certificate.SubjectName,
                X509SignatureGenerator.CreateForRSA(issuer.Key, RSASignaturePadding.Pkcs1),
                from,
                to,
                RandomNumberGenerator.GetBytes(8));
        return new Issuer(certificate, key);
    }
}
