using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class CmsSignerCertificateReaderTests
{
    private readonly CmsSignerCertificateReader _reader = new();

    [Fact]
    public void ReadSignerName_SignerAfterTheAuthority_ReturnsTheSignerNotTheAuthority()
    {
        var contents = FakeCms.Build(new FakeCertificate("ROSSI MARIO", AuthorityName: "Fake Authority", AuthorityFirst: true));

        _reader.ReadSignerName(contents).Should().Be("ROSSI MARIO");
    }

    [Fact]
    public void ReadSignerName_SignerBeforeTheAuthority_ReturnsTheSigner()
    {
        var contents = FakeCms.Build(new FakeCertificate("ROSSI MARIO", AuthorityName: "Fake Authority", AuthorityFirst: false));

        _reader.ReadSignerName(contents).Should().Be("ROSSI MARIO");
    }

    [Fact]
    public void ReadSignerName_SelfSignedSignerAlone_ReturnsTheSigner()
    {
        var contents = FakeCms.Build(new FakeCertificate("Mario Rossi"));

        _reader.ReadSignerName(contents).Should().Be("Mario Rossi");
    }

    [Fact]
    public void ReadSignerName_SignerFoundBySubjectKeyIdentifier_ReturnsTheSigner()
    {
        var contents = FakeCms.Build(new FakeCertificate(
            "ROSSI MARIO", AuthorityName: "Fake Authority", Reference: SignerReference.SubjectKeyIdentifier));

        _reader.ReadSignerName(contents).Should().Be("ROSSI MARIO");
    }

    [Fact]
    public void ReadSignerName_SignerInfoPointsToNoCertificate_ReturnsNullInsteadOfGuessing()
    {
        var contents = FakeCms.Build(new FakeCertificate(
            "ROSSI MARIO", AuthorityName: "Fake Authority", Reference: SignerReference.Unmatched));

        _reader.ReadSignerName(contents).Should().BeNull();
    }

    [Fact]
    public void ReadSignerName_NoSignerInfo_ReturnsNull()
    {
        var contents = FakeCms.Build(new FakeCertificate("ROSSI MARIO", Reference: SignerReference.NoSignerInfo));

        _reader.ReadSignerName(contents).Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x01 })]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void ReadSignerName_BytesThatAreNotASignedDataStructure_ReturnsNullAndThrowsNothing(byte[] contents)
    {
        var act = () => _reader.ReadSignerName(contents);

        act.Should().NotThrow();
        _reader.ReadSignerName(contents).Should().BeNull();
    }

    [Fact]
    public void ReadSignerName_EmptyBytes_ReturnsNull()
    {
        _reader.ReadSignerName(ReadOnlyMemory<byte>.Empty).Should().BeNull();
    }

    [Fact]
    public void ReadSignerName_TruncatedStructure_ReturnsNullAndThrowsNothing()
    {
        var contents = FakeCms.Build(new FakeCertificate("ROSSI MARIO"), zeroPadding: 0);

        var act = () => _reader.ReadSignerName(contents.AsMemory(0, contents.Length / 2));

        act.Should().NotThrow();
        _reader.ReadSignerName(contents.AsMemory(0, contents.Length / 2)).Should().BeNull();
    }

    [Fact]
    public void ReadSignerName_TrailingZeroPadding_IsAccepted()
    {
        var contents = FakeCms.Build(new FakeCertificate("Mario Rossi"), zeroPadding: 4096);

        _reader.ReadSignerName(contents).Should().Be("Mario Rossi");
    }
}
