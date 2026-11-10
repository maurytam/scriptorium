using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Scriptorium.Infrastructure.Tests.Parsing;

/// <summary>How the signer-info of a fake signature points to the signer's certificate.</summary>
public enum SignerReference
{
    IssuerAndSerial,
    SubjectKeyIdentifier,

    /// <summary>The signer-info exists but names an issuer and serial that match no certificate in the structure.</summary>
    Unmatched,

    /// <summary>The structure has certificates but an empty set of signer-infos.</summary>
    NoSignerInfo,
}

/// <summary>
/// The certificates of one fake signature. The signer's certificate is issued by the authority when there is
/// one; <see cref="AuthorityFirst"/> decides which of the two comes first in the certificate set.
/// </summary>
public sealed record FakeCertificate(
    string SignerName,
    string? AuthorityName = null,
    bool AuthorityFirst = true,
    SignerReference Reference = SignerReference.IssuerAndSerial);

/// <summary>
/// Builds a CMS signed-data structure with fake certificates, made only with the .NET runtime
/// (<see cref="CertificateRequest"/> and <see cref="AsnWriter"/>; no package). The signature value is meaningless.
/// </summary>
public static class FakeCms
{
    private const string SignedDataOid = "1.2.840.113549.1.7.2";
    private const string DataOid = "1.2.840.113549.1.7.1";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string EcdsaSha256Oid = "1.2.840.10045.4.3.2";

    private static readonly DateTimeOffset NotBefore = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NotAfter = new(2036, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SignerSerial = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0x01];
    private static readonly byte[] UnknownSerial = [0x7F, 0x7E, 0x7D, 0x7C];

    private static readonly Asn1Tag ExplicitContent = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag CertificateSet = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag KeyIdentifier = new(TagClass.ContextSpecific, 0);

    /// <summary>The structure followed by <paramref name="zeroPadding"/> zero bytes, as a signature field reserves room.</summary>
    public static byte[] Build(FakeCertificate fake, int zeroPadding = 32)
    {
        var authority = fake.AuthorityName is null ? null : NewCertificate(fake.AuthorityName, null);
        var signer = NewCertificate(fake.SignerName, authority);
        var certificates = authority is null ? [signer] : OrderedPair(fake, authority, signer);

        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(SignedDataOid);
            using (writer.PushSequence(ExplicitContent))
            {
                WriteSignedData(writer, certificates, signer, fake.Reference);
            }
        }

        return [.. writer.Encode(), .. new byte[zeroPadding]];
    }

    private static X509Certificate2[] OrderedPair(FakeCertificate fake, X509Certificate2 authority, X509Certificate2 signer) =>
        fake.AuthorityFirst ? [authority, signer] : [signer, authority];

    private static X509Certificate2 NewCertificate(string commonName, X509Certificate2? issuer)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(issuer is null, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return issuer is null
            ? request.CreateSelfSigned(NotBefore, NotAfter)
            : request.Create(issuer, NotBefore, NotAfter, SignerSerial);
    }

    private static void WriteSignedData(AsnWriter writer, X509Certificate2[] certificates, X509Certificate2 signer, SignerReference reference)
    {
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            using (writer.PushSetOf())
            {
                WriteAlgorithm(writer, Sha256Oid, withNullParameters: true);
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(DataOid);
            }

            using (writer.PushSetOf(CertificateSet))
            {
                foreach (var certificate in certificates)
                {
                    writer.WriteEncodedValue(certificate.RawData);
                }
            }

            using (writer.PushSetOf())
            {
                WriteSignerInfo(writer, signer, reference);
            }
        }
    }

    private static void WriteSignerInfo(AsnWriter writer, X509Certificate2 signer, SignerReference reference)
    {
        if (reference == SignerReference.NoSignerInfo)
        {
            return;
        }

        using (writer.PushSequence())
        {
            writer.WriteInteger(reference == SignerReference.SubjectKeyIdentifier ? 3 : 1);
            WriteSignerIdentifier(writer, signer, reference);
            WriteAlgorithm(writer, Sha256Oid, withNullParameters: true);
            WriteAlgorithm(writer, EcdsaSha256Oid, withNullParameters: false);
            writer.WriteOctetString([0x01, 0x02, 0x03]);
        }
    }

    private static void WriteSignerIdentifier(AsnWriter writer, X509Certificate2 signer, SignerReference reference)
    {
        if (reference == SignerReference.SubjectKeyIdentifier)
        {
            var extension = signer.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single();
            writer.WriteOctetString(extension.SubjectKeyIdentifierBytes.Span, KeyIdentifier);
            return;
        }

        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(signer.IssuerName.RawData);
            writer.WriteIntegerUnsigned(reference == SignerReference.Unmatched ? UnknownSerial : signer.SerialNumberBytes.Span);
        }
    }

    private static void WriteAlgorithm(AsnWriter writer, string oid, bool withNullParameters)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(oid);
            if (withNullParameters)
            {
                writer.WriteNull();
            }
        }
    }
}
