using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/{id:guid}/ask", AskAsync);

        return app;
    }

    private static async Task<IResult> AskAsync(
        Guid id, AskRequestDto? body, DocumentQuestionService questionService, CancellationToken ct)
    {
        var result = await questionService.AskAsync(id, body?.Question ?? string.Empty, ct);

        return result.IsSuccess
            ? Results.Ok(AskResponseDto.From(result))
            : ToFailureResponse(result.FailureKind, result.Error);
    }

    private static IResult ToFailureResponse(AskFailureKind? kind, string error)
    {
        var body = new ErrorDto(error);
        return kind switch
        {
            AskFailureKind.InvalidQuestion => Results.BadRequest(body),
            AskFailureKind.NotFound => Results.NotFound(body),
            AskFailureKind.NotReady => Results.Conflict(body),
            _ => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
