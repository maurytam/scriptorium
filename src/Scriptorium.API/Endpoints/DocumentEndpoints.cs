using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents");

        // Local single-user app with no authentication or cookies, so anti-forgery does not apply.
        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/text", GetTextAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile? file, DocumentUploadService uploadService, CancellationToken ct)
    {
        if (file is null)
        {
            return Results.BadRequest(new ErrorDto("No file was provided."));
        }

        await using var content = file.OpenReadStream();
        var result = await uploadService.UploadAsync(content, file.FileName, file.Length, isPrivate: false, ct);

        return result.IsSuccess
            ? Results.Accepted($"/api/documents/{result.Value.Id}", UploadedDocumentDto.From(result.Value))
            : Results.Json(new ErrorDto(result.Error), statusCode: StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, IDocumentRepository repository, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(id, ct);
        return document is null ? Results.NotFound() : Results.Ok(DocumentDetailsDto.From(document));
    }

    private static async Task<IResult> GetTextAsync(Guid id, IDocumentRepository repository, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(id, ct);
        if (document is null)
        {
            return Results.NotFound();
        }

        var text = document.Status == DocumentStatus.Ready
            ? await repository.GetExtractedTextAsync(id, ct)
            : null;

        return text is null
            ? Results.Conflict(new ErrorDto($"Document is not ready. Current status: {document.Status.ToString().ToLowerInvariant()}."))
            : Results.Ok(ExtractedTextDto.From(text));
    }
}
