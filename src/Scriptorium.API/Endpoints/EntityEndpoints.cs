using Scriptorium.Core.Dtos;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class EntityEndpoints
{
    public static IEndpointRouteBuilder MapEntityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/{id:guid}/entities", ExtractAsync);

        return app;
    }

    private static async Task<IResult> ExtractAsync(
        Guid id, EntityRequestDto? body, DocumentEntityService entityService, CancellationToken ct)
    {
        // The preference is checked first: a value that means nothing is refused before anything else is read.
        if (!ModelPreferenceParser.TryParse(body?.Preference, out var preference))
        {
            return Results.BadRequest(new ErrorDto("The preference must be auto, local or cloud."));
        }

        var result = await entityService.ExtractAsync(id, preference, ct);

        return result.IsSuccess
            ? Results.Ok(EntityResponseDto.From(result))
            : AskFailureResponses.From(result.FailureKind, result.Error);
    }
}
