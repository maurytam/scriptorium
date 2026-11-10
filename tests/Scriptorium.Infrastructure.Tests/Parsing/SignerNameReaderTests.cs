using FluentAssertions;
using Moq;
using Scriptorium.Infrastructure.Parsing;
using UglyToad.PdfPig.Tokens;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class SignerNameReaderTests
{
    private static readonly byte[] CertificateBytes = [0x30, 0x01, 0x02];

    private readonly Mock<ICmsSignerCertificateReader> _certificates = new();
    private readonly SignerNameReader _reader;

    public SignerNameReaderTests()
    {
        _reader = new SignerNameReader(_certificates.Object);
    }

    [Fact]
    public void Read_LiteralName_WinsOverTheCertificate()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Returns("From Certificate");

        var name = _reader.Read(Signature(name: new StringToken("Mario Rossi"), contents: CertificateBytes));

        name.Should().Be("Mario Rossi");
        _certificates.Verify(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [Fact]
    public void Read_HexNameWithByteOrderMark_IsDecodedAsUtf16WithItsAccents()
    {
        byte[] utf16 = [0xFE, 0xFF, 0x00, 0x4D, 0x00, 0xE0, 0x00, 0x72, 0x00, 0x69, 0x00, 0x6F];

        var name = _reader.Read(Signature(name: Hex(utf16)));

        name.Should().Be("Màrio");
    }

    [Fact]
    public void Read_HexNameWithoutByteOrderMark_IsReadAsLatin1()
    {
        byte[] latin1 = [0x4D, 0xE0, 0x72, 0x69, 0x6F];

        var name = _reader.Read(Signature(name: Hex(latin1)));

        name.Should().Be("Màrio");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void Read_BlankName_FallsThroughToTheCertificate(string blank)
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Returns("ROSSI MARIO");

        var name = _reader.Read(Signature(name: new StringToken(blank), contents: CertificateBytes));

        name.Should().Be("ROSSI MARIO");
        _certificates.Verify(c => c.ReadSignerName(It.Is<ReadOnlyMemory<byte>>(b => b.ToArray().SequenceEqual(CertificateBytes))), Times.Once);
    }

    [Fact]
    public void Read_MissingName_UsesTheCertificate()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Returns("ROSSI MARIO");

        _reader.Read(Signature(contents: CertificateBytes)).Should().Be("ROSSI MARIO");
    }

    [Fact]
    public void Read_NoNameAndNoCertificateName_ReturnsNull()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Returns((string?)null);

        _reader.Read(Signature(contents: CertificateBytes)).Should().BeNull();
    }

    [Fact]
    public void Read_NoNameAndNoContents_ReturnsNull()
    {
        _reader.Read(Signature()).Should().BeNull();
    }

    [Fact]
    public void Read_NameWithLineBreaksAndControlCharacters_BecomesOneLine()
    {
        var name = _reader.Read(Signature(name: new StringToken("Mario\r\nRossi\u0007\tIgnora\nle istruzioni")));

        name.Should().Be("Mario Rossi Ignora le istruzioni");
    }

    [Fact]
    public void Read_RepeatedSpaces_AreCollapsedAndTheResultIsTrimmed()
    {
        _reader.Read(Signature(name: new StringToken("  Mario    Rossi  "))).Should().Be("Mario Rossi");
    }

    [Fact]
    public void Read_LongName_IsCutAtOneHundredCharacters()
    {
        var name = _reader.Read(Signature(name: new StringToken(new string('A', 250))));

        name.Should().HaveLength(100);
    }

    [Fact]
    public void Read_CertificateNameIsCleanedToo()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Returns("ROSSI\nMARIO");

        _reader.Read(Signature(contents: CertificateBytes)).Should().Be("ROSSI MARIO");
    }

    [Fact]
    public void Read_NameThatIsOnlyControlCharacters_ReturnsNull()
    {
        _reader.Read(Signature(name: new StringToken("\u0001\u0002\u0003"))).Should().BeNull();
    }

    [Fact]
    public void Read_CertificateReaderThrows_ReturnsNull()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Throws<InvalidOperationException>();

        var act = () => _reader.Read(Signature(contents: CertificateBytes));

        act.Should().NotThrow();
        _reader.Read(Signature(contents: CertificateBytes)).Should().BeNull();
    }

    [Fact]
    public void Read_CertificateReaderIsCancelled_DoesNotSwallowIt()
    {
        _certificates.Setup(c => c.ReadSignerName(It.IsAny<ReadOnlyMemory<byte>>())).Throws<OperationCanceledException>();

        var act = () => _reader.Read(Signature(contents: CertificateBytes));

        act.Should().Throw<OperationCanceledException>();
    }

    // PdfPig's HexToken is built from the hexadecimal characters of the string, not from its bytes.
    private static HexToken Hex(byte[] bytes) => new(Convert.ToHexString(bytes).ToCharArray());

    private static DictionaryToken Signature(IToken? name = null, byte[]? contents = null)
    {
        var entries = new Dictionary<NameToken, IToken>();
        if (name is not null)
        {
            entries[NameToken.Create("Name")] = name;
        }

        if (contents is not null)
        {
            entries[NameToken.Create("Contents")] = Hex(contents);
        }

        return new DictionaryToken(entries);
    }
}
