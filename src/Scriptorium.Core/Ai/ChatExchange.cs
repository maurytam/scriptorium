namespace Scriptorium.Core.Ai;

/// <summary>One earlier turn of a conversation: what the user asked and what the model answered.</summary>
public sealed record ChatExchange(string Question, string Answer);
