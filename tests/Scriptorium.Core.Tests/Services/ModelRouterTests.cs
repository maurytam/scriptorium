using FluentAssertions;
using Moq;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class ModelRouterTests
{
    private const string CloudNotAvailable =
        "The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.";

    private const string PrivateRefused =
        "This document is private, so it can only be answered on this computer. Choose Auto or Local.";

    private const int Threshold = 2;

    public enum CloudState
    {
        Off,
        OnNotConfigured,
        OnConfigured
    }

    public enum Level
    {
        Below = 1,
        AtThreshold = 2,
        Above = 3
    }

    private readonly Mock<IAIProvider> _local = new(MockBehavior.Strict);
    private readonly Mock<ICloudAIProvider> _cloud = new(MockBehavior.Strict);

    // The eight rows of the decision table in data-model.md, one by one.
    [Theory]
    [InlineData(true, ModelPreference.Cloud, CloudState.OnConfigured, Level.Above, null, RoutingFailureKind.PrivateDocumentCloudRefused)]
    [InlineData(true, ModelPreference.Cloud, CloudState.Off, Level.Below, null, RoutingFailureKind.PrivateDocumentCloudRefused)]
    [InlineData(true, ModelPreference.Auto, CloudState.OnConfigured, Level.Above, ModelBackend.Local, null)]
    [InlineData(true, ModelPreference.Local, CloudState.OnConfigured, Level.Above, ModelBackend.Local, null)]
    [InlineData(false, ModelPreference.Local, CloudState.OnConfigured, Level.Above, ModelBackend.Local, null)]
    [InlineData(false, ModelPreference.Cloud, CloudState.OnConfigured, Level.Below, ModelBackend.Cloud, null)]
    [InlineData(false, ModelPreference.Cloud, CloudState.Off, Level.Above, null, RoutingFailureKind.CloudNotAvailable)]
    [InlineData(false, ModelPreference.Cloud, CloudState.OnNotConfigured, Level.Above, null, RoutingFailureKind.CloudNotAvailable)]
    [InlineData(false, ModelPreference.Auto, CloudState.OnConfigured, Level.Above, ModelBackend.Cloud, null)]
    [InlineData(false, ModelPreference.Auto, CloudState.OnConfigured, Level.AtThreshold, ModelBackend.Local, null)]
    [InlineData(false, ModelPreference.Auto, CloudState.OnConfigured, Level.Below, ModelBackend.Local, null)]
    [InlineData(false, ModelPreference.Auto, CloudState.Off, Level.Above, ModelBackend.Local, null)]
    [InlineData(false, ModelPreference.Auto, CloudState.OnNotConfigured, Level.Above, ModelBackend.Local, null)]
    public void Route_DecisionTable_RowByRow(
        bool isPrivate, ModelPreference preference, CloudState cloud, Level level,
        ModelBackend? expectedBackend, RoutingFailureKind? expectedFailure)
    {
        var result = Route(isPrivate, preference, cloud, level);

        AssertOutcome(result, expectedBackend, expectedFailure);
    }

    // Every combination of privacy, preference, cloud state and score against an independent oracle.
    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Route_EveryCombination_GivesTheOutcomeOfTheTable(
        bool isPrivate, ModelPreference preference, CloudState cloud, Level level)
    {
        var (backend, failure) = Oracle(isPrivate, preference, cloud, level);

        AssertOutcome(Route(isPrivate, preference, cloud, level), backend, failure);
    }

    public static IEnumerable<object[]> AllCombinations() =>
        from isPrivate in new[] { false, true }
        from preference in Enum.GetValues<ModelPreference>()
        from cloud in Enum.GetValues<CloudState>()
        from level in Enum.GetValues<Level>()
        select new object[] { isPrivate, preference, cloud, level };

    [Fact]
    public void Route_RefusalForAPrivateDocumentWithCloud_IsTheSameWhateverTheCloudState()
    {
        foreach (var cloud in Enum.GetValues<CloudState>())
        {
            var result = Route(true, ModelPreference.Cloud, cloud, Level.Above);

            result.FailureKind.Should().Be(RoutingFailureKind.PrivateDocumentCloudRefused);
            result.Error.Should().Be(PrivateRefused);
        }
    }

    [Fact]
    public void Route_CloudNotAvailable_HasTheExplicitMessage()
    {
        var result = Route(false, ModelPreference.Cloud, CloudState.Off, Level.Below);

        result.FailureKind.Should().Be(RoutingFailureKind.CloudNotAvailable);
        result.Error.Should().Be(CloudNotAvailable);
    }

    [Fact]
    public void Route_LocalSuccess_HandsBackTheLocalProviderAndCloudSuccessTheCloudOne()
    {
        Route(false, ModelPreference.Local, CloudState.OnConfigured, Level.Above).Provider.Should().BeSameAs(_local.Object);

        Route(false, ModelPreference.Cloud, CloudState.OnConfigured, Level.Below).Provider.Should().BeSameAs(_cloud.Object);
    }

    [Theory]
    [InlineData(0, 1, ModelBackend.Cloud)]
    [InlineData(0, 0, ModelBackend.Local)]
    [InlineData(5, 5, ModelBackend.Local)]
    [InlineData(4, 5, ModelBackend.Cloud)]
    public void Route_AutoComparesTheScoreWithTheConfiguredThreshold(int threshold, int score, ModelBackend expected)
    {
        var router = Router(CloudState.OnConfigured, threshold);

        var result = router.Route(new RoutingRequest(false, ModelPreference.Auto, score));

        result.Backend.Should().Be(expected);
    }

    [Fact]
    public void Route_NeverCallsAProvider()
    {
        foreach (var cloud in Enum.GetValues<CloudState>())
        {
            Route(false, ModelPreference.Auto, cloud, Level.Above);
        }

        _local.VerifyNoOtherCalls();
        _cloud.Verify(c => c.CompleteAsync(It.IsAny<AiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ModelBackend? Backend, RoutingFailureKind? Failure) Oracle(
        bool isPrivate, ModelPreference preference, CloudState cloud, Level level)
    {
        if (isPrivate && preference == ModelPreference.Cloud)
        {
            return (null, RoutingFailureKind.PrivateDocumentCloudRefused);
        }

        if (isPrivate || preference == ModelPreference.Local)
        {
            return (ModelBackend.Local, null);
        }

        var usable = cloud == CloudState.OnConfigured;
        if (preference == ModelPreference.Cloud)
        {
            return usable ? (ModelBackend.Cloud, null) : (null, RoutingFailureKind.CloudNotAvailable);
        }

        return (usable && level == Level.Above ? ModelBackend.Cloud : ModelBackend.Local, null);
    }

    private static void AssertOutcome(
        RoutingResult result, ModelBackend? expectedBackend, RoutingFailureKind? expectedFailure)
    {
        if (expectedBackend is not null)
        {
            result.IsSuccess.Should().BeTrue();
            result.Backend.Should().Be(expectedBackend);
            result.FailureKind.Should().BeNull();
            return;
        }

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(expectedFailure);
        result.Provider.Should().BeNull();
    }

    private RoutingResult Route(bool isPrivate, ModelPreference preference, CloudState cloud, Level level) =>
        Router(cloud, Threshold).Route(new RoutingRequest(isPrivate, preference, (int)level));

    private ModelRouter Router(CloudState cloud, int threshold)
    {
        // A key may be present while the switch is off: the switch must win.
        _cloud.SetupGet(c => c.IsConfigured).Returns(cloud != CloudState.OnNotConfigured);
        var options = new RoutingOptions { CloudEnabled = cloud != CloudState.Off, ComplexityThreshold = threshold };
        return new ModelRouter(_local.Object, _cloud.Object, options);
    }
}
