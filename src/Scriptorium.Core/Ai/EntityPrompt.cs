namespace Scriptorium.Core.Ai;

/// <summary>
/// The request to send to the model for an entity extraction, whether the document had to be cut to fit, and the exact text
/// the model was given, which is what every entity is checked against.
/// </summary>
public sealed record EntityPrompt(AiRequest Request, bool Truncated, string TextRead);
