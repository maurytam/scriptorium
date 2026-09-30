using Scriptorium.Core.Entities;

namespace Scriptorium.Core.Dtos;

public sealed record UploadedDocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    long FileSizeBytes,
    DateTimeOffset UploadDate,
    bool IsPrivate,
    string Status)
{
    public static UploadedDocumentDto From(Document document) => new(
        document.Id,
        document.FileName,
        document.FileType,
        document.FileSizeBytes,
        document.UploadDate,
        document.IsPrivate,
        document.Status.ToString().ToLowerInvariant());
}

public sealed record DocumentDetailsDto(
    Guid Id,
    string FileName,
    string FileType,
    long FileSizeBytes,
    DateTimeOffset UploadDate,
    bool IsPrivate,
    string Status,
    string? FailureReason)
{
    public static DocumentDetailsDto From(Document document) => new(
        document.Id,
        document.FileName,
        document.FileType,
        document.FileSizeBytes,
        document.UploadDate,
        document.IsPrivate,
        document.Status.ToString().ToLowerInvariant(),
        document.FailureReason);
}

public sealed record DocumentSummaryDto(
    Guid Id,
    string FileName,
    string FileType,
    DateTimeOffset UploadDate,
    bool IsPrivate,
    string Status)
{
    public static DocumentSummaryDto From(Document document) => new(
        document.Id,
        document.FileName,
        document.FileType,
        document.UploadDate,
        document.IsPrivate,
        document.Status.ToString().ToLowerInvariant());
}

public sealed record ExtractedTextDto(Guid DocumentId, string Content, DateTimeOffset ExtractedAt)
{
    public static ExtractedTextDto From(ExtractedText text) => new(text.DocumentId, text.Content, text.ExtractedAt);
}

public sealed record ErrorDto(string Error);

public sealed record UploadLimitsDto(long MaxSizeBytes);
