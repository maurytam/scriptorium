using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

public sealed class TextPreviewResult
{
    private TextPreviewResult(
        string? content, bool truncated, int totalCharacters, TextPreviewFailureKind? failureKind, string? error)
    {
        Content = content;
        Truncated = truncated;
        TotalCharacters = totalCharacters;
        FailureKind = failureKind;
        Error = error;
    }

    /// <summary>The text of the document, or its first characters when it is longer than the preview limit.</summary>
    public string? Content { get; }

    /// <summary>True when <see cref="Content"/> is only the beginning of the text.</summary>
    public bool Truncated { get; }

    /// <summary>The length of the whole text, in characters.</summary>
    public int TotalCharacters { get; }

    public TextPreviewFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Content))]
    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => Content is not null;

    public static TextPreviewResult Success(string content, bool truncated, int totalCharacters) =>
        new(content, truncated, totalCharacters, null, null);

    public static TextPreviewResult Failure(TextPreviewFailureKind kind, string error) =>
        new(null, false, 0, kind, error);
}
