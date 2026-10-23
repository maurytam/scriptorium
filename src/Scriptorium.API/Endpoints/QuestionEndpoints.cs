using Scriptorium.Core.Ai;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/{id:guid}/ask", AskAsync);
        app.MapGet("/api/documents/ask/limits", (QaOptions options) => Results.Ok(AskLimitsDto.From(options)));

        return app;
    }

    private static async Task<IResult> AskAsync(
        Guid id, AskRequestDto? body, DocumentQuestionService questionService, CancellationToken ct)
    {
        var history = ToExchanges(body?.History);
        if (history is null)
        {
            return Results.BadRequest(new ErrorDto("Every earlier exchange needs a question and an answer."));
        }

        var result = await questionService.AskAsync(id, body?.Question ?? string.Empty, history, ct);

        return result.IsSuccess
            ? Results.Ok(AskResponseDto.From(result))
            : ToFailureResponse(result.FailureKind, result.Error);
    }

    /// <summary>Maps the earlier exchanges; null when one of them is incomplete. No history is an empty list.</summary>
    private static List<ChatExchange>? ToExchanges(IReadOnlyList<ChatExchangeDto?>? history)
    {
        var exchanges = new List<ChatExchange>();
        foreach (var entry in history ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry?.Question) || string.IsNullOrWhiteSpace(entry.Answer))
            {
                return null;
            }

            exchanges.Add(new ChatExchange(entry.Question, entry.Answer));
        }

        return exchanges;
    }

    private static IResult ToFailureResponse(AskFailureKind? kind, string error)
    {
        var body = new ErrorDto(error);
        return kind switch
        {
            AskFailureKind.InvalidQuestion => Results.BadRequest(body),
            AskFailureKind.NotFound => Results.NotFound(body),
            AskFailureKind.NotReady => Results.Conflict(body),
            AskFailureKind.ModelUnavailable => Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable),
            AskFailureKind.Timeout => Results.Json(body, statusCode: StatusCodes.Status504GatewayTimeout),
            _ => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
