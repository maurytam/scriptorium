using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Scriptorium.Infrastructure.Parsing;

/// <summary>
/// Reads the signer's certificate out of a CMS signed-data structure with the runtime's own
/// <c>System.Formats.Asn1</c> and <c>X509CertificateLoader</c> (no package). Nothing is verified:
/// the structure is only read, and the certificate the first signer-info points to is the signer.
/// </summary>
public sealed class CmsSignerCertificateReader : ICmsSignerCertificateReader
{
    private const string SignedDataOid = "1.2.840.113549.1.7.2";

    private static readonly Asn1Tag ContextZero = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag ContextOne = new(TagClass.ContextSpecific, 1);

    public string? ReadSignerName(ReadOnlyMemory<byte> contents)
    {
        var certificates = new List<X509Certificate2>();
        try
        {
            var signedData = OpenSignedData(contents);
            if (signedData is null)
            {
                return null;
            }

            SkipHeader(signedData);
            certificates.AddRange(ReadCertificates(signedData));
            var signer = FindSigner(certificates, signedData.ReadSetOf());
            return Clean(signer?.GetNameInfo(X509NameType.SimpleName, false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
        finally
        {
            certificates.ForEach(certificate => certificate.Dispose());
        }
    }

    private static AsnReader? OpenSignedData(ReadOnlyMemory<byte> contents)
    {
        var contentInfo = new AsnReader(contents, AsnEncodingRules.BER).ReadSequence();
        return contentInfo.ReadObjectIdentifier() == SignedDataOid
            ? contentInfo.ReadSequence(ContextZero).ReadSequence()
            : null;
    }

    // Version, digest algorithms and the encapsulated content: nothing of them is needed.
    private static void SkipHeader(AsnReader signedData)
    {
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
    }

    private static List<X509Certificate2> ReadCertificates(AsnReader signedData)
    {
        var certificates = new List<X509Certificate2>();
        if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(ContextZero))
        {
            var set = signedData.ReadSetOf(ContextZero);
            while (set.HasData)
            {
                var certificate = TryLoad(set.ReadEncodedValue());
                if (certificate is not null)
                {
                    certificates.Add(certificate);
                }
            }
        }

        if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(ContextOne))
        {
            signedData.ReadEncodedValue();
        }

        return certificates;
    }

    // A certificate that cannot be loaded must not stop the others from being read.
    private static X509Certificate2? TryLoad(ReadOnlyMemory<byte> encoded)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(encoded.Span);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static X509Certificate2? FindSigner(List<X509Certificate2> certificates, AsnReader signerInfos)
    {
        if (!signerInfos.HasData)
        {
            return null;
        }

        var signerInfo = signerInfos.ReadSequence();
        signerInfo.ReadEncodedValue();
        if (signerInfo.PeekTag().HasSameClassAndValue(ContextZero))
        {
            var keyIdentifier = signerInfo.ReadOctetString(ContextZero);
            return certificates.FirstOrDefault(certificate => HasKeyIdentifier(certificate, keyIdentifier));
        }

        var issuerAndSerial = signerInfo.ReadSequence();
        var issuer = issuerAndSerial.ReadEncodedValue();
        var serial = issuerAndSerial.ReadIntegerBytes();
        return certificates.FirstOrDefault(certificate => HasIssuerAndSerial(certificate, issuer.Span, serial.Span));
    }

    private static bool HasKeyIdentifier(X509Certificate2 certificate, byte[] keyIdentifier)
    {
        var extension = certificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault();
        return extension is not null && extension.SubjectKeyIdentifierBytes.Span.SequenceEqual(keyIdentifier);
    }

    private static bool HasIssuerAndSerial(X509Certificate2 certificate, ReadOnlySpan<byte> issuer, ReadOnlySpan<byte> serial) =>
        issuer.SequenceEqual(certificate.IssuerName.RawData) &&
        certificate.SerialNumberBytes.Span.TrimStart((byte)0).SequenceEqual(serial.TrimStart((byte)0));

    private static string? Clean(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;
}
