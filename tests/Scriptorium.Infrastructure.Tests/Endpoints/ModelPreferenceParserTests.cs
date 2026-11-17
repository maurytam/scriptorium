using FluentAssertions;
using Scriptorium.API.Endpoints;
using Scriptorium.Core.Enums;

namespace Scriptorium.Infrastructure.Tests.Endpoints;

public class ModelPreferenceParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryParse_NullOrEmpty_IsAutoAndValid(string? value)
    {
        ModelPreferenceParser.TryParse(value, out var preference).Should().BeTrue();

        preference.Should().Be(ModelPreference.Auto);
    }

    [Theory]
    [InlineData("auto", ModelPreference.Auto)]
    [InlineData("AUTO", ModelPreference.Auto)]
    [InlineData("local", ModelPreference.Local)]
    [InlineData("Local", ModelPreference.Local)]
    [InlineData("cloud", ModelPreference.Cloud)]
    [InlineData("CLOUD", ModelPreference.Cloud)]
    public void TryParse_KnownValueInAnyLetterCase_GivesThePreference(string value, ModelPreference expected)
    {
        ModelPreferenceParser.TryParse(value, out var preference).Should().BeTrue();

        preference.Should().Be(expected);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("cloud ")]
    [InlineData("turbo")]
    public void TryParse_AnythingElse_IsNotValid(string value)
    {
        ModelPreferenceParser.TryParse(value, out _).Should().BeFalse();
    }
}
