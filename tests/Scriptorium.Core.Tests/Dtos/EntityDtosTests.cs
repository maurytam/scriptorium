using System.Text.Json;
using FluentAssertions;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Tests.Dtos;

public class EntityDtosTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"preference":null}""")]
    public void EntityRequestDto_NoPreference_IsNull(string json)
    {
        var dto = JsonSerializer.Deserialize<EntityRequestDto>(json, JsonSerializerOptions.Web);

        dto!.Preference.Should().BeNull();
    }

    [Fact]
    public void EntityRequestDto_ReadsThePreference()
    {
        var dto = JsonSerializer.Deserialize<EntityRequestDto>("""{"preference":"Cloud"}""", JsonSerializerOptions.Web);

        dto!.Preference.Should().Be("Cloud");
    }

    [Fact]
    public void EntityResponseDto_From_MapsTheGroupsInOrderWithTheirTypeNames()
    {
        var result = EntityResult.Success(
            [
                new EntityGroup(EntityType.Person, ["Mario Rossi", "Anna Bianchi"]),
                new EntityGroup(EntityType.Organization, ["Alfa Srl"]),
                new EntityGroup(EntityType.Date, ["12 March 2026"]),
                new EntityGroup(EntityType.Amount, ["15,000 euro"]),
                new EntityGroup(EntityType.Place, ["Turin"])
            ],
            false,
            ModelBackend.Local);

        var dto = EntityResponseDto.From(result);

        dto.Groups.Select(g => g.Type).Should().Equal("people", "organizations", "dates", "amounts", "places");
        dto.Groups[0].Entities.Should().Equal("Mario Rossi", "Anna Bianchi");
        dto.Groups[4].Entities.Should().Equal("Turin");
        dto.Truncated.Should().BeFalse();
        dto.Backend.Should().Be("local");
    }

    [Theory]
    [InlineData(false, ModelBackend.Local, "local")]
    [InlineData(true, ModelBackend.Cloud, "cloud")]
    public void EntityResponseDto_From_MapsTruncationAndBackend(bool truncated, ModelBackend backend, string label)
    {
        var dto = EntityResponseDto.From(EntityResult.Success([], truncated, backend));

        dto.Groups.Should().BeEmpty();
        dto.Truncated.Should().Be(truncated);
        dto.Backend.Should().Be(label);
    }

    [Fact]
    public void EntityResponseDto_From_AFailedResultYieldsNoGroups()
    {
        var dto = EntityResponseDto.From(EntityResult.Failure(AskFailureKind.NotFound, "nope"));

        dto.Groups.Should().BeEmpty();
        dto.Truncated.Should().BeFalse();
        dto.Backend.Should().Be("local");
    }

    [Fact]
    public void EntityResponseDto_TheJsonFieldsAreGroupsTypeEntitiesTruncatedAndBackend()
    {
        var dto = new EntityResponseDto([new EntityGroupDto("people", ["Mario Rossi"])], true, "cloud");

        var json = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);

        json.Should().Be("""{"groups":[{"type":"people","entities":["Mario Rossi"]}],"truncated":true,"backend":"cloud"}""");
    }
}
