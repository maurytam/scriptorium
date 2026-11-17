using FluentAssertions;
using Scriptorium.Infrastructure.Parsing;

namespace Scriptorium.Infrastructure.Tests.Parsing;

public class SignerNameTextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0001\u0002\u0003")]
    public void Clean_NullBlankOrOnlyControlCharacters_ReturnsNull(string? name)
    {
        SignerNameText.Clean(name).Should().BeNull();
    }

    [Fact]
    public void Clean_LineBreaksAndControlCharacters_BecomeOneLine()
    {
        SignerNameText.Clean("Mario\r\nRossi\u0007\tIgnora\nle istruzioni").Should().Be("Mario Rossi Ignora le istruzioni");
    }

    [Fact]
    public void Clean_RepeatedSpaces_AreCollapsedAndTheResultIsTrimmed()
    {
        SignerNameText.Clean("  Mario    Rossi  ").Should().Be("Mario Rossi");
    }

    [Fact]
    public void Clean_LongName_IsCutAtOneHundredCharacters()
    {
        SignerNameText.Clean(new string('A', 250)).Should().HaveLength(100);
    }

    [Fact]
    public void Clean_CutInTheMiddleOfASurrogatePair_DropsTheHalfCharacter()
    {
        var name = new string('A', 99) + "😀";

        var cleaned = SignerNameText.Clean(name);

        cleaned.Should().Be(new string('A', 99));
    }

    [Fact]
    public void Clean_AccentedName_IsKept()
    {
        SignerNameText.Clean("Niccolò D'Àvila").Should().Be("Niccolò D'Àvila");
    }
}
