using FluentAssertions;
using Moq;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class RoutingResultTests
{
    [Theory]
    [InlineData(ModelBackend.Local)]
    [InlineData(ModelBackend.Cloud)]
    public void Success_CarriesTheBackendAndTheProvider(ModelBackend backend)
    {
        var provider = new Mock<IAIProvider>().Object;

        var result = RoutingResult.Success(backend, provider);

        result.IsSuccess.Should().BeTrue();
        result.Backend.Should().Be(backend);
        result.Provider.Should().BeSameAs(provider);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(RoutingFailureKind.CloudNotAvailable)]
    [InlineData(RoutingFailureKind.PrivateDocumentCloudRefused)]
    public void Failure_CarriesKindAndMessageAndNoProvider(RoutingFailureKind kind)
    {
        var result = RoutingResult.Failure(kind, "nope");

        result.IsSuccess.Should().BeFalse();
        result.Backend.Should().BeNull();
        result.Provider.Should().BeNull();
        result.FailureKind.Should().Be(kind);
        result.Error.Should().Be("nope");
    }
}
