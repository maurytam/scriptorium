namespace Scriptorium.Core.Ai;

/// <summary>The request to send to the model, and whether the document had to be cut to fit.</summary>
public sealed record QuestionPrompt(AiRequest Request, bool Truncated);
