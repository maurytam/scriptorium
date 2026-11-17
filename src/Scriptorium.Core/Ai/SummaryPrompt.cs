namespace Scriptorium.Core.Ai;

/// <summary>The request to send to the model for a summary, and whether the document had to be cut to fit.</summary>
public sealed record SummaryPrompt(AiRequest Request, bool Truncated);
