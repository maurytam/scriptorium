using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>The use case "ask a question about a document".</summary>
public sealed class DocumentQuestionService
{
    private readonly IDocumentRepository _repository;
    private readonly ModelRouter _router;
    private readonly ComplexityScorer _scorer;
    private readonly QuestionPromptBuilder _promptBuilder;
    private readonly QaOptions _options;

    public DocumentQuestionService(
        IDocumentRepository repository,
        ModelRouter router,
        ComplexityScorer scorer,
        QuestionPromptBuilder promptBuilder,
        QaOptions options)
    {
        _repository = repository;
        _router = router;
        _scorer = scorer;
        _promptBuilder = promptBuilder;
        _options = options;
    }

    public async Task<AskResult> AskAsync(
        Guid documentId, string question, IReadOnlyList<ChatExchange>? history, ModelPreference preference, CancellationToken ct)
    {
        var trimmed = question.Trim();
        var invalid = ValidateQuestion(trimmed);
        if (invalid is not null)
        {
            return invalid;
        }

        var text = await ReadyDocumentText.LoadAsync(_repository, documentId, ct);
        if (!text.IsSuccess)
        {
            return AskResult.Failure(text.FailureKind, text.Error);
        }

        var route = Route(text.Value, text.IsPrivate, trimmed, history, preference);
        return route.IsSuccess
            ? await AnswerAsync(route.Provider, route.Backend.Value, text.Value, trimmed, history, ct)
            : AskFailureMapper.FromRouting(route.FailureKind.Value, route.Error);
    }

    private RoutingResult Route(
        string documentText, bool isPrivate, string question, IReadOnlyList<ChatExchange>? history, ModelPreference preference)
    {
        var score = _scorer.Score(question, documentText.Length, history?.Count ?? 0);
        return _router.Route(new RoutingRequest(isPrivate, preference, score.Value));
    }

    private async Task<AskResult> AnswerAsync(
        IAIProvider provider, ModelBackend backend, string documentText, string question,
        IReadOnlyList<ChatExchange>? history, CancellationToken ct)
    {
        var prompt = _promptBuilder.Build(documentText, question, history);
        var answer = await provider.CompleteAsync(prompt.Request, ct);
        return answer.IsSuccess
            ? AskResult.Success(answer.Text, prompt.Truncated, backend)
            : AskFailureMapper.FromProvider(answer.FailureKind.Value, answer.Error);
    }

    private AskResult? ValidateQuestion(string question)
    {
        if (question.Length == 0)
        {
            return AskResult.Failure(AskFailureKind.InvalidQuestion, "The question is empty.");
        }

        return question.Length > _options.MaxQuestionLength
            ? AskResult.Failure(AskFailureKind.InvalidQuestion, $"The question is too long. The limit is {_options.MaxQuestionLength} characters.")
            : null;
    }
}
