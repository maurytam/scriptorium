using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Ai;

/// <summary>One entity as the model named it, not yet checked against the document.</summary>
public sealed record EntityCandidate(EntityType Type, string Text);
