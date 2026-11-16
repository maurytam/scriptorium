using System.Text.RegularExpressions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Services;

/// <summary>
/// Scores how demanding a question is, from 0 to 5: one point for each of five signals that is present.
/// It receives the question and two numbers, never the text of the document.
/// </summary>
public sealed class ComplexityScorer
{
    private const int LongQuestionCharacters = 400;
    private const int SeveralQuestionMarks = 2;
    private const int LongReadingPercentOfLimit = 75;
    private const int ConversationExchanges = 3;

    // The fixed list, English and Italian. Whole words or phrases, any letter case, no stemming, not configurable.
    private static readonly string[] ReasoningPhrases =
    [
        "compare", "explain why", "analyze", "differences", "implications", "pros and cons",
        "confronta", "perche", "perché", "perchè", "analizza", "differenze", "implicazioni", "pro e contro"
    ];

    private static readonly Regex ReasoningWords = BuildReasoningWords();

    private readonly QaOptions _options;

    public ComplexityScorer(QaOptions options)
    {
        _options = options;
    }

    /// <param name="question">The question, already trimmed.</param>
    /// <param name="documentLength">The length of the document text; only the part the model reads counts.</param>
    /// <param name="earlierExchanges">How many question and answer pairs came before in the conversation.</param>
    public ComplexityScore Score(string question, int documentLength, int earlierExchanges)
    {
        var signals = ComplexitySignals.None;
        signals |= Signal(question.Length >= LongQuestionCharacters, ComplexitySignals.LongQuestion);
        signals |= Signal(question.Count(c => c == '?') >= SeveralQuestionMarks, ComplexitySignals.SeveralQuestions);
        signals |= Signal(IsLongReading(documentLength), ComplexitySignals.LongReading);
        signals |= Signal(earlierExchanges >= ConversationExchanges, ComplexitySignals.ConversationUnderWay);
        signals |= Signal(ReasoningWords.IsMatch(question), ComplexitySignals.ReasoningWords);
        return new ComplexityScore(signals);
    }

    private static ComplexitySignals Signal(bool present, ComplexitySignals signal) =>
        present ? signal : ComplexitySignals.None;

    private bool IsLongReading(int documentLength)
    {
        var limit = _options.MaxDocumentCharacters;
        var read = Math.Min(documentLength, limit);
        return limit > 0 && (long)read * 100 >= (long)limit * LongReadingPercentOfLimit;
    }

    private static Regex BuildReasoningWords()
    {
        var alternatives = ReasoningPhrases.Select(p => string.Join(@"\s+", p.Split(' ').Select(Regex.Escape)));
        var pattern = $@"\b(?:{string.Join("|", alternatives)})\b";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
