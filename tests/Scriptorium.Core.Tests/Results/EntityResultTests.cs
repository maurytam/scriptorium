using FluentAssertions;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Results;

public class EntityResultTests
{
    [Fact]
    public void Success_CarriesTheGroupsTheTruncatedFlagAndTheBackend()
    {
        var groups = new[] { new EntityGroup(EntityType.Person, ["Mario Rossi"]) };

        var result = EntityResult.Success(groups, true, ModelBackend.Cloud);

        result.IsSuccess.Should().BeTrue();
        result.Groups.Should().Equal(groups);
        result.Truncated.Should().BeTrue();
        result.Backend.Should().Be(ModelBackend.Cloud);
        result.FailureKind.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithNoGroupsIsStillASuccess()
    {
        var result = EntityResult.Success([], false, ModelBackend.Local);

        result.IsSuccess.Should().BeTrue();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Failure_CarriesAKindAndAMessageAndAnEmptyListOfGroups()
    {
        var result = EntityResult.Failure(AskFailureKind.NotEnoughText, "too short");

        result.IsSuccess.Should().BeFalse();
        result.FailureKind.Should().Be(AskFailureKind.NotEnoughText);
        result.Error.Should().Be("too short");
        result.Groups.Should().BeEmpty();
        result.Backend.Should().Be(ModelBackend.Local);
    }
}
