using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;

namespace Scriptorium.API.Endpoints;

/// <summary>Maps a failed question or summary to its HTTP status; the body is always <c>{ "error": message }</c>.</summary>
public static class AskFailureResponses
{
    public static IResult From(AskFailureKind? kind, string error)
    {
        var body = new ErrorDto(error);
        return kind switch
        {
            AskFailureKind.InvalidQuestion => Results.BadRequest(body),
            AskFailureKind.NotFound => Results.NotFound(body),
            AskFailureKind.NotReady or AskFailureKind.PreferenceNotAllowed or AskFailureKind.NotEnoughText => Results.Conflict(body),
            AskFailureKind.ModelUnavailable => Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable),
            AskFailureKind.Timeout => Results.Json(body, statusCode: StatusCodes.Status504GatewayTimeout),
            _ => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
