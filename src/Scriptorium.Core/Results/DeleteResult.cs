using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

public sealed class DeleteResult
{
    private DeleteResult(DeleteFailureKind? failureKind, string? error)
    {
        FailureKind = failureKind;
        Error = error;
    }

    public DeleteFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => FailureKind is null;

    public static DeleteResult Success() => new(null, null);

    public static DeleteResult Failure(DeleteFailureKind kind, string error) => new(kind, error);
}
