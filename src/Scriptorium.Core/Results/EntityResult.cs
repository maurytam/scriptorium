using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

/// <summary>The outcome of an entity extraction: the groups found (possibly none), or the reason it failed.</summary>
public sealed class EntityResult
{
    private EntityResult(IReadOnlyList<EntityGroup> groups, bool truncated, ModelBackend backend, AskFailureKind? failureKind, string? error)
    {
        Groups = groups;
        Truncated = truncated;
        Backend = backend;
        FailureKind = failureKind;
        Error = error;
    }

    /// <summary>The non-empty groups, in the fixed order of the types. Empty when nothing was found and when the extraction failed.</summary>
    public IReadOnlyList<EntityGroup> Groups { get; }

    /// <summary>True when only the beginning of a document that was too long to be read at once was used.</summary>
    public bool Truncated { get; }

    /// <summary>Which side produced the reply. A failed result keeps the default.</summary>
    public ModelBackend Backend { get; }

    public AskFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => FailureKind is null;

    public static EntityResult Success(IReadOnlyList<EntityGroup> groups, bool truncated, ModelBackend backend) =>
        new(groups, truncated, backend, null, null);

    public static EntityResult Failure(AskFailureKind kind, string error) =>
        new([], false, ModelBackend.Local, kind, error);
}
