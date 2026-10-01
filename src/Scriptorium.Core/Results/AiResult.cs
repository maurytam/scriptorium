using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

public sealed class AiResult
{
    private AiResult(string? text, AiFailureKind? failureKind, string? error)
    {
        Text = text;
        FailureKind = failureKind;
        Error = error;
    }

    public string? Text { get; }

    public AiFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Text))]
    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => Text is not null;

    public static AiResult Success(string text) => new(text, null, null);

    public static AiResult Failure(AiFailureKind kind, string error) => new(null, kind, error);
}
