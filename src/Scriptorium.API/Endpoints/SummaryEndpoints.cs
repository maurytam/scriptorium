using Scriptorium.Core.Dtos;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class SummaryEndpoints
{
    public static IEndpointRouteBuilder MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/{id:guid}/summary", SummarizeAsync);

        return app;
    }

    private static async Task<IResult> SummarizeAsync(
        Guid id, SummaryRequestDto? body, DocumentSummaryService summaryService, CancellationToken ct)
    {
        // The preference is checked first: a value that means nothing is refused before anything else is read.
        if (!ModelPreferenceParser.TryParse(body?.Preference, out var preference))
        {
            return Results.BadRequest(new ErrorDto("The preference must be auto, local or cloud."));
        }

        var result = await summaryService.SummarizeAsync(id, preference, ct);

        return result.IsSuccess
            ? Results.Ok(SummaryResponseDto.From(result))
            : AskFailureResponses.From(result.FailureKind, result.Error);
    }
}
