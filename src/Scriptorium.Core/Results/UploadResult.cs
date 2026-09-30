using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Entities;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

public sealed class UploadResult
{
    private UploadResult(Document? document, UploadFailureKind? failureKind, string? error)
    {
        Document = document;
        FailureKind = failureKind;
        Error = error;
    }

    public Document? Document { get; }

    public UploadFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Document))]
    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => Document is not null;

    public static UploadResult Success(Document document) => new(document, null, null);

    public static UploadResult Failure(UploadFailureKind kind, string error) => new(null, kind, error);
}
