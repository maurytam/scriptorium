using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>The use case "ask a question about a document".</summary>
public sealed class DocumentQuestionService
{
    private readonly IDocumentRepository _repository;
    private readonly IAIProvider _provider;
    private readonly QuestionPromptBuilder _promptBuilder;
    private readonly QaOptions _options;

    public DocumentQuestionService(
        IDocumentRepository repository,
        IAIProvider provider,
        QuestionPromptBuilder promptBuilder,
        QaOptions options)
    {
        _repository = repository;
        _provider = provider;
        _promptBuilder = promptBuilder;
        _options = options;
    }

    public async Task<AskResult> AskAsync(Guid documentId, string question, CancellationToken ct)
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

        var prompt = _promptBuilder.Build(text.Value, trimmed);
        var answer = await _provider.CompleteAsync(prompt.Request, ct);
        return answer.IsSuccess
            ? AskResult.Success(answer.Text, prompt.Truncated)
            : AskResult.Failure(AskFailureKind.Internal, "The answer could not be produced.");
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
            : DocumentText.Found(extracted.Content);
    }

    private sealed class DocumentText
    {
        private DocumentText(string? value, AskFailureKind failureKind, string? error)
        {
            Value = value;
            FailureKind = failureKind;
            Error = error;
        }

        public string? Value { get; }

        public AskFailureKind FailureKind { get; }

        public string? Error { get; }

        [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Value))]
        [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => Value is not null;

        public static DocumentText Found(string value) => new(value, default, null);

        public static DocumentText Failed(AskFailureKind kind, string error) => new(null, kind, error);
    }
}
