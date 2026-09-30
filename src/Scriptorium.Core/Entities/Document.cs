using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Entities;

public sealed class Document
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DateTimeOffset UploadDate { get; set; }

    public bool IsPrivate { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Processing;

    public string? FailureReason { get; set; }
}
