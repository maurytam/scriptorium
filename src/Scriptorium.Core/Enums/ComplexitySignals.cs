namespace Scriptorium.Core.Enums;

/// <summary>The five signals of the complexity rule. A question scores one point for each one that is present.</summary>
[Flags]
public enum ComplexitySignals
{
    None = 0,
    LongQuestion = 1,
    SeveralQuestions = 2,
    LongReading = 4,
    ConversationUnderWay = 8,
    ReasoningWords = 16
}
