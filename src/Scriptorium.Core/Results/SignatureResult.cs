using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

/// <summary>The outcome of reading the signatures of a document: the entries (possibly none), or the reason it failed.</summary>
public sealed class SignatureResult
{
    private SignatureResult(IReadOnlyList<SignatureEntry> signatures, AskFailureKind? failureKind, string? error)
    {
        Signatures = signatures;
        FailureKind = failureKind;
        Error = error;
    }

    /// <summary>The signatures in the order of the file. Empty when the document has none and when the read failed.</summary>
    public IReadOnlyList<SignatureEntry> Signatures { get; }

    public AskFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => FailureKind is null;

    public static SignatureResult Success(IReadOnlyList<SignatureEntry> signatures) => new(signatures, null, null);

    public static SignatureResult Failure(AskFailureKind kind, string error) => new([], kind, error);
}
