using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;

namespace Scriptorium.Core.Services;

/// <summary>The text of a ready document, or the reason it cannot be had. It is its own result.</summary>
public sealed class ReadyDocumentText
{
    private ReadyDocumentText(string? value, bool isPrivate, string fileType, AskFailureKind failureKind, string? error)
    {
        Value = value;
        IsPrivate = isPrivate;
        FileType = fileType;
        FailureKind = failureKind;
        Error = error;
    }

    public string? Value { get; }

    public bool IsPrivate { get; }

    /// <summary>The type recorded for the document at upload (<c>pdf</c>, <c>docx</c>...); empty when the load failed.</summary>
    public string FileType { get; }

    public AskFailureKind FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Value is not null;

    /// <summary>Loads the document and, only when it is ready, its extracted text.</summary>
    public static async Task<ReadyDocumentText> LoadAsync(IDocumentRepository repository, Guid documentId, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            return Failed(AskFailureKind.NotFound, "Document not found.");
        }

        if (document.Status != DocumentStatus.Ready)
        {
            return Failed(
                AskFailureKind.NotReady,
                $"The document is not ready. Current status: {document.Status.ToString().ToLowerInvariant()}.");
        }

        var extracted = await repository.GetExtractedTextAsync(documentId, ct);
        return extracted is null
            ? Failed(AskFailureKind.Internal, "The text of the document is not available.")
            : new ReadyDocumentText(extracted.Content, document.IsPrivate, document.FileType, default, null);
    }

    private static ReadyDocumentText Failed(AskFailureKind kind, string error) => new(null, false, string.Empty, kind, error);
}
