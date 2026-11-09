using System.Text;

namespace Scriptorium.Infrastructure.Tests.Parsing;

/// <summary>
/// One fake digital signature. <see cref="SigningDate"/> is written as-is into the /M entry
/// (null = no /M). <see cref="IsSigned"/> = false leaves an empty placeholder field with no /V.
/// <see cref="IsNested"/> puts the field under a parent field (/Kids).
/// </summary>
public sealed record FakeSignature(string? SigningDate = null, bool IsSigned = true, bool IsNested = false);

/// <summary>
/// Builds fake PDFs with signature fields as raw PDF text (the same technique as the encrypted PDF in
/// <see cref="PdfDocumentParserTests"/>), because PdfDocumentBuilder cannot create form fields.
/// The page text must not contain parentheses; an empty page text gives a page with no text (a scan).
/// </summary>
public static class SignedPdfFixture
{
    private const int FirstSignatureObject = 10;
    private const int ObjectsPerSignature = 3;

    public static byte[] Build(string pageText, params FakeSignature[] signatures) =>
        Encoding.ASCII.GetBytes(Document(pageText, signatures, listFieldsInReverse: false));

    /// <summary>Same as <see cref="Build"/>, but /Fields lists the last signature first.</summary>
    public static byte[] BuildWithFieldsListedInReverse(string pageText, params FakeSignature[] signatures) =>
        Encoding.ASCII.GetBytes(Document(pageText, signatures, listFieldsInReverse: true));

    /// <summary>A form whose /Fields points to an object that does not exist.</summary>
    public static byte[] BuildWithMalformedForm(string pageText) =>
        Encoding.ASCII.GetBytes(Assemble(pageText, "99 0 R", string.Empty, string.Empty, FirstSignatureObject));

    private static string Document(string pageText, FakeSignature[] signatures, bool listFieldsInReverse)
    {
        var objects = new StringBuilder();
        var formFields = new List<int>();
        var widgets = new List<int>();
        for (var i = 0; i < signatures.Length; i++)
        {
            var field = FirstSignatureObject + i * ObjectsPerSignature;
            objects.Append(SignatureObjects(field, signatures[i]));
            formFields.Add(signatures[i].IsNested ? field + 2 : field);
            widgets.Add(field);
        }

        if (listFieldsInReverse)
        {
            formFields.Reverse();
        }

        var size = FirstSignatureObject + signatures.Length * ObjectsPerSignature;
        return Assemble(pageText, References(formFields), References(widgets), objects.ToString(), size);
    }

    private static string SignatureObjects(int field, FakeSignature signature)
    {
        var dictionary = field + 1;
        var parent = field + 2;
        var text = Field(field, signature.IsSigned ? dictionary : null, signature.IsNested ? parent : null);
        if (signature.IsSigned)
        {
            text += SignatureDictionary(dictionary, signature.SigningDate);
        }

        if (signature.IsNested)
        {
            text += $"{parent} 0 obj\n<< /T (Group{parent}) /Kids [{field} 0 R] >>\nendobj\n";
        }

        return text;
    }

    private static string Field(int number, int? valueObject, int? parentObject) =>
        $"{number} 0 obj\n<< /Type /Annot /Subtype /Widget /FT /Sig /T (Sig{number}) /Rect [0 0 0 0] /P 3 0 R" +
        (valueObject is null ? string.Empty : $" /V {valueObject} 0 R") +
        (parentObject is null ? string.Empty : $" /Parent {parentObject} 0 R") +
        " >>\nendobj\n";

    private static string SignatureDictionary(int number, string? signingDate) =>
        $"{number} 0 obj\n<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached" +
        (signingDate is null ? string.Empty : $" /M ({signingDate})") +
        " /ByteRange [0 10 20 30] /Contents <00> >>\nendobj\n";

    private static string References(IEnumerable<int> objects) =>
        string.Join(" ", objects.Select(n => $"{n} 0 R"));

    private static string Assemble(string pageText, string formFields, string widgets, string signatureObjects, int size)
    {
        var content = pageText.Length == 0 ? string.Empty : $"BT /F1 12 Tf 25 700 Td ({pageText}) Tj ET";
        return "%PDF-1.7\n" +
            $"1 0 obj\n<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{formFields}] /SigFlags 3 >> >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R " +
            "/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> " +
            $"/Annots [{widgets}] >>\nendobj\n" +
            $"4 0 obj\n<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n" +
            signatureObjects +
            $"trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n0\n%%EOF\n";
    }
}
