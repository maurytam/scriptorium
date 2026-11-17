using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class AskFailureMapperTests
{
    [Fact]
    public void FromRouting_PrivateDocumentCloudRefused_IsPreferenceNotAllowedWithTheRoutersText()
    {
        var result = AskFailureMapper.FromRouting(RoutingFailureKind.PrivateDocumentCloudRefused, "A private document stays here.");

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.PreferenceNotAllowed);
        result.Error.Should().Be("A private document stays here.");
    }

    [Fact]
    public void FromRouting_CloudNotAvailable_IsModelUnavailableWithTheRoutersText()
    {
        var result = AskFailureMapper.FromRouting(RoutingFailureKind.CloudNotAvailable, "The cloud model is switched off.");

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        result.Error.Should().Be("The cloud model is switched off.");
    }

    [Fact]
    public void FromProvider_Unavailable_IsModelUnavailableWithTheProvidersText()
    {
        var result = AskFailureMapper.FromProvider(AiFailureKind.Unavailable, "Ollama is not running.");

        result.FailureKind.Should().Be(AskFailureKind.ModelUnavailable);
        result.Error.Should().Be("Ollama is not running.");
    }

    [Fact]
    public void FromProvider_Timeout_IsTimeoutWithTheProvidersText()
    {
        var result = AskFailureMapper.FromProvider(AiFailureKind.Timeout, "The model took too long.");

        result.FailureKind.Should().Be(AskFailureKind.Timeout);
        result.Error.Should().Be("The model took too long.");
    }

    [Fact]
    public void FromProvider_Failed_IsInternalWithAGenericTextAndNoDetail()
    {
        var result = AskFailureMapper.FromProvider(AiFailureKind.Failed, "stack trace with secret detail");

        result.FailureKind.Should().Be(AskFailureKind.Internal);
        result.Error.Should().Be("The answer could not be produced.");
        result.Error.Should().NotContain("secret");
    }
}
