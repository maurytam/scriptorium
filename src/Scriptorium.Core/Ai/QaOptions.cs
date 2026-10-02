namespace Scriptorium.Core.Ai;

/// <summary>Limits applied when asking questions about a document (bound from the "Qa" configuration section).</summary>
public sealed record QaOptions
{
    public int MaxQuestionLength { get; init; } = 2000;

    public int MaxDocumentCharacters { get; init; } = 4000;

    public int MaxHistoryExchanges { get; init; } = 10;

    public int MaxHistoryCharacters { get; init; } = 3000;
}
