using Scriptorium.Core.Ai;

namespace Scriptorium.Core.Services;

/// <summary>What the model's reply turned out to be.</summary>
public enum EntityReplyKind
{
    /// <summary>At least one line could be read.</summary>
    Entities,

    /// <summary>The model said there is nothing (the word NONE).</summary>
    None,

    /// <summary>No line could be read and the reply was not NONE.</summary>
    Unreadable
}

/// <summary>The reading of a reply: what it was and the candidate entities found in it.</summary>
public sealed record EntityReply(EntityReplyKind Kind, IReadOnlyList<EntityCandidate> Candidates);
