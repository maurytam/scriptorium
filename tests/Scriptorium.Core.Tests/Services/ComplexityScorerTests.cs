using FluentAssertions;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Services;

namespace Scriptorium.Core.Tests.Services;

public class ComplexityScorerTests
{
    private const int LongDocument = 20_000;

    private readonly ComplexityScorer _scorer = new(new QaOptions());

    [Theory]
    [InlineData(399, false)]
    [InlineData(400, true)]
    public void Score_LongQuestion_FiresFromFourHundredCharacters(int length, bool fires)
    {
        var score = _scorer.Score(new string('a', length), 0, 0);

        score.Signals.HasFlag(ComplexitySignals.LongQuestion).Should().Be(fires);
        score.Value.Should().Be(fires ? 1 : 0);
    }

    [Theory]
    [InlineData("What is it?", false)]
    [InlineData("What is it? Who signed?", true)]
    [InlineData("What? Who? When?", true)]
    public void Score_SeveralQuestions_FiresFromTwoQuestionMarks(string question, bool fires)
    {
        var score = _scorer.Score(question, 0, 0);

        score.Signals.HasFlag(ComplexitySignals.SeveralQuestions).Should().Be(fires);
    }

    [Theory]
    [InlineData(5_999, false)]
    [InlineData(6_000, true)]
    [InlineData(50_000, true)]
    public void Score_LongReading_FiresWhenSeventyFivePercentOfTheReadableLimitIsReached(int documentLength, bool fires)
    {
        var score = _scorer.Score("Who signed", documentLength, 0);

        score.Signals.HasFlag(ComplexitySignals.LongReading).Should().Be(fires);
    }

    [Theory]
    [InlineData(1_000, 749, false)]
    [InlineData(1_000, 750, true)]
    public void Score_LongReading_FollowsTheConfiguredReadableLimit(int maxDocumentCharacters, int documentLength, bool fires)
    {
        var scorer = new ComplexityScorer(new QaOptions { MaxDocumentCharacters = maxDocumentCharacters });

        scorer.Score("Who signed", documentLength, 0).Signals.HasFlag(ComplexitySignals.LongReading).Should().Be(fires);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void Score_ConversationUnderWay_FiresFromThreeEarlierExchanges(int earlierExchanges, bool fires)
    {
        var score = _scorer.Score("And then", 0, earlierExchanges);

        score.Signals.HasFlag(ComplexitySignals.ConversationUnderWay).Should().Be(fires);
    }

    [Theory]
    [InlineData("compare")]
    [InlineData("explain why")]
    [InlineData("analyze")]
    [InlineData("differences")]
    [InlineData("implications")]
    [InlineData("pros and cons")]
    [InlineData("confronta")]
    [InlineData("perche")]
    [InlineData("perché")]
    [InlineData("perchè")]
    [InlineData("analizza")]
    [InlineData("differenze")]
    [InlineData("implicazioni")]
    [InlineData("pro e contro")]
    public void Score_ReasoningWords_EveryWordOfTheFixedListFires(string word)
    {
        var score = _scorer.Score($"Please {word} these two", 0, 0);

        score.Signals.Should().Be(ComplexitySignals.ReasoningWords);
        score.Value.Should().Be(1);
    }

    [Theory]
    [InlineData("COMPARE the two")]
    [InlineData("Perché è così")]
    [InlineData("PERCHÉ è così")]
    [InlineData("PERCHÈ è così")]
    [InlineData("explain   why it happened")]
    [InlineData("list the PROS  AND  CONS")]
    [InlineData("Pro e contro del contratto")]
    public void Score_ReasoningWords_IgnoreLetterCaseAndRunsOfSpaces(string question)
    {
        _scorer.Score(question, 0, 0).Signals.Should().Be(ComplexitySignals.ReasoningWords);
    }

    [Theory]
    [InlineData("It was compared yesterday")]
    [InlineData("The analyzer is broken")]
    [InlineData("This is incomparable")]
    [InlineData("Who signed the contract")]
    public void Score_ReasoningWords_AWordInsideAnotherWordDoesNotFire(string question)
    {
        _scorer.Score(question, 0, 0).Signals.Should().Be(ComplexitySignals.None);
    }

    [Fact]
    public void Score_SeveralMatchingWords_CountAsOnePoint()
    {
        var score = _scorer.Score("Compare and analyze the differences", 0, 0);

        score.Signals.Should().Be(ComplexitySignals.ReasoningWords);
        score.Value.Should().Be(1);
    }

    [Fact]
    public void Score_NoSignal_IsZero()
    {
        var score = _scorer.Score("Who signed", 100, 1);

        score.Signals.Should().Be(ComplexitySignals.None);
        score.Value.Should().Be(0);
    }

    [Fact]
    public void Score_AllFiveSignals_IsFiveAndNamesThemAll()
    {
        var question = "Compare the penalties? Explain why they differ? " + new string('x', 400);

        var score = _scorer.Score(question, LongDocument, 3);

        score.Value.Should().Be(5);
        score.Signals.Should().Be(
            ComplexitySignals.LongQuestion | ComplexitySignals.SeveralQuestions | ComplexitySignals.LongReading
            | ComplexitySignals.ConversationUnderWay | ComplexitySignals.ReasoningWords);
    }

    [Fact]
    public void Score_ExampleOne_AShortLookupInALongDocumentScoresOne()
    {
        var score = _scorer.Score("Qual è il codice finale?", LongDocument, 0);

        score.Signals.Should().Be(ComplexitySignals.LongReading);
        score.Value.Should().Be(1);
    }

    [Fact]
    public void Score_ExampleTwo_ComparisonInALongDocumentScoresTwo()
    {
        var score = _scorer.Score("Confronta le penali e spiega perché differiscono?", LongDocument, 0);

        score.Signals.Should().Be(ComplexitySignals.ReasoningWords | ComplexitySignals.LongReading);
        score.Value.Should().Be(2);
    }

    [Fact]
    public void Score_ExampleThree_ALongConversationWithSeveralQuestionsScoresFour()
    {
        var score = _scorer.Score("Compare the penalties? And explain the terms?", LongDocument, 3);

        score.Signals.Should().Be(
            ComplexitySignals.ReasoningWords | ComplexitySignals.SeveralQuestions | ComplexitySignals.LongReading
            | ComplexitySignals.ConversationUnderWay);
        score.Value.Should().Be(4);
    }

    [Fact]
    public void Score_SameInputs_GiveTheSameScore()
    {
        var first = _scorer.Score("Compare the penalties? Why?", LongDocument, 4);
        var second = _scorer.Score("Compare the penalties? Why?", LongDocument, 4);

        second.Should().Be(first);
    }
}
