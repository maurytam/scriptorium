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

        var text = await LoadTextAsync(documentId, ct);
        if (!text.IsSuccess)
        {
            return AskResult.Failure(text.FailureKind, text.Error);
        }

        var route = Route(text.Value, text.IsPrivate, trimmed, history, preference);
        return route.IsSuccess
            ? await AnswerAsync(route.Provider, route.Backend.Value, text.Value, trimmed, history, ct)
            : ToRoutingFailure(route.FailureKind.Value, route.Error);
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
            : ToFailure(answer.FailureKind.Value, answer.Error);
    }

    // The router words both refusals itself; a private document asked with Cloud is the one that is a conflict.
    private static AskResult ToRoutingFailure(RoutingFailureKind kind, string error) => kind switch
    {
        RoutingFailureKind.PrivateDocumentCloudRefused => AskResult.Failure(AskFailureKind.PreferenceNotAllowed, error),
        _ => AskResult.Failure(AskFailureKind.ModelUnavailable, error)
    };

    // The provider words the two failures the user can act on; anything else stays generic, with no details.
    private static AskResult ToFailure(AiFailureKind kind, string error) => kind switch
    {
        AiFailureKind.Unavailable => AskResult.Failure(AskFailureKind.ModelUnavailable, error),
        AiFailureKind.Timeout => AskResult.Failure(AskFailureKind.Timeout, error),
        _ => AskResult.Failure(AskFailureKind.Internal, "The answer could not be produced.")
    };

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

    private async Task<DocumentText> LoadTextAsync(Guid documentId, CancellationToken ct)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            return DocumentText.Failed(AskFailureKind.NotFound, "Document not found.");
        }

        if (document.Status != DocumentStatus.Ready)
        {
            return DocumentText.Failed(
                AskFailureKind.NotReady,
                $"The document is not ready. Current status: {document.Status.ToString().ToLowerInvariant()}.");
        }

        var extracted = await _repository.GetExtractedTextAsync(documentId, ct);
        return extracted is null
            ? DocumentText.Failed(AskFailureKind.Internal, "The text of the document is not available.")
            : DocumentText.Found(extracted.Content, document.IsPrivate);
    }

    private sealed class DocumentText
    {
        private DocumentText(string? value, bool isPrivate, AskFailureKind failureKind, string? error)
        {
            Value = value;
            IsPrivate = isPrivate;
            FailureKind = failureKind;
            Error = error;
        }

        public string? Value { get; }

        public bool IsPrivate { get; }

        public AskFailureKind FailureKind { get; }

        public string? Error { get; }

        [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Value))]
        [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => Value is not null;

        public static DocumentText Found(string value, bool isPrivate) => new(value, isPrivate, default, null);

        public static DocumentText Failed(AskFailureKind kind, string error) => new(null, false, kind, error);
    }
}
