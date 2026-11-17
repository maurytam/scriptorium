using Scriptorium.Core.Dtos;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class SignatureEndpoints
{
    public static IEndpointRouteBuilder MapSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/documents/{id:guid}/signatures", ReadAsync);

        return app;
    }

    private static async Task<IResult> ReadAsync(Guid id, DocumentSignatureService service, CancellationToken ct)
    {
        var result = await service.ReadAsync(id, ct);

        return result.IsSuccess
            ? Results.Ok(SignaturesResponseDto.From(result))
            : AskFailureResponses.From(result.FailureKind, result.Error);
    }
}
