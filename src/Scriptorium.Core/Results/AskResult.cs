using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

public sealed class AskResult
{
    private AskResult(string? answer, bool truncated, AskFailureKind? failureKind, string? error)
    {
        Answer = answer;
        Truncated = truncated;
        FailureKind = failureKind;
        Error = error;
    }

    public string? Answer { get; }

    /// <summary>True when only the beginning of a document that was too long to be read at once was used.</summary>
    public bool Truncated { get; }

    public AskFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Answer))]
    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => Answer is not null;

    public static AskResult Success(string answer, bool truncated) => new(answer, truncated, null, null);

    public static AskResult Failure(AskFailureKind kind, string error) => new(null, false, kind, error);
}
