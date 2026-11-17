using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Dtos;

/// <summary>The optional body of an entity request. No body, or no preference, is Auto.</summary>
public sealed record EntityRequestDto(string? Preference = null);

public sealed record EntityGroupDto(string Type, IReadOnlyList<string> Entities);

public sealed record EntityResponseDto(IReadOnlyList<EntityGroupDto> Groups, bool Truncated, string Backend = "local")
{
    /// <summary>Maps a successful result; a failed one has no groups and yields an empty list.</summary>
    public static EntityResponseDto From(EntityResult result) =>
        new(
            result.Groups.Select(g => new EntityGroupDto(TypeName(g.Type), g.Entities)).ToList(),
            result.Truncated,
            result.Backend == ModelBackend.Cloud ? "cloud" : "local");

    private static string TypeName(EntityType type) => type switch
    {
        EntityType.Person => "people",
        EntityType.Organization => "organizations",
        EntityType.Date => "dates",
        EntityType.Amount => "amounts",
        _ => "places"
    };
}
