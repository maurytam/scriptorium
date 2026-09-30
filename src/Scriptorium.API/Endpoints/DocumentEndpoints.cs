using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Scriptorium.Core.Dtos;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Services;

namespace Scriptorium.API.Endpoints;

public static class DocumentEndpoints
{
    /// <summary>Headroom above the file limit for multipart boundaries and form fields.</summary>
    public const long MultipartOverheadBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app, long maxFileSizeBytes)
    {
        var group = app.MapGroup("/api/documents");

        // Local single-user app with no authentication or cookies, so anti-forgery does not apply.
        // The request limit sits above the file limit so oversized files reach the service check
        // and get the documented JSON 413 instead of a bare framework rejection.
        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(maxFileSizeBytes + MultipartOverheadBytes));
        group.MapGet("/", ListAsync);
        group.MapGet("/limits", () => Results.Ok(new UploadLimitsDto(maxFileSizeBytes)));
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/text", GetTextAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        DocumentUploadService uploadService,
        CancellationToken ct,
        [FromForm] bool isPrivate = false)
    {
        if (file is null)
        {
            return Results.BadRequest(new ErrorDto("No file was provided."));
        }

        await using var content = file.OpenReadStream();
        var result = await uploadService.UploadAsync(content, file.FileName, file.Length, isPrivate, ct);

        return result.IsSuccess
            ? Results.Accepted($"/api/documents/{result.Document.Id}", UploadedDocumentDto.From(result.Document))
            : ToFailureResponse(result.FailureKind, result.Error);
    }

    private static IResult ToFailureResponse(UploadFailureKind? kind, string error)
    {
        var body = new ErrorDto(error);
        return kind switch
        {
            UploadFailureKind.UnsupportedType => Results.BadRequest(body),
            UploadFailureKind.Duplicate => Results.Conflict(body),
            UploadFailureKind.FileTooLarge => Results.Json(body, statusCode: StatusCodes.Status413PayloadTooLarge),
            _ => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> ListAsync(IDocumentRepository repository, CancellationToken ct)
    {
        var documents = await repository.GetAllAsync(ct);
        return Results.Ok(documents.Select(DocumentSummaryDto.From));
    }

    private static async Task<IResult> GetByIdAsync(Guid id, IDocumentRepository repository, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(id, ct);
        return document is null ? Results.NotFound() : Results.Ok(DocumentDetailsDto.From(document));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, DocumentDeletionService deletionService, CancellationToken ct)
    {
        var result = await deletionService.DeleteAsync(id, ct);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        var body = new ErrorDto(result.Error);
        return result.FailureKind switch
        {
            DeleteFailureKind.NotFound => Results.NotFound(body),
            DeleteFailureKind.StillProcessing => Results.Conflict(body),
            _ => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError)
        };
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
