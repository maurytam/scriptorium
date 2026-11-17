using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Results;

/// <summary>The checked entities of one type: no repeats, written as the document writes them, in the order they first appear.</summary>
public sealed record EntityGroup(EntityType Type, IReadOnlyList<string> Entities);
