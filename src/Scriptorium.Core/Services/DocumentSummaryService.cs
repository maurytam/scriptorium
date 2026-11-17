using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>The use case "summarise a ready document". The summary is produced each time and never stored.</summary>
public sealed class DocumentSummaryService
{
    /// <summary>Under this many characters (trimmed) there is nothing worth summarising, and a model would invent.</summary>
    public const int MinimumCharacters = 50;

    private const string TooLittleText = "The document has too little text to summarise.";
    private const string NotProduced = "The answer could not be produced.";

    private readonly IDocumentRepository _repository;
    private readonly ModelRouter _router;
    private readonly ComplexityScorer _scorer;
    private readonly SummaryPromptBuilder _promptBuilder;

    public DocumentSummaryService(
        IDocumentRepository repository, ModelRouter router, ComplexityScorer scorer, SummaryPromptBuilder promptBuilder)
    {
        _repository = repository;
        _router = router;
        _scorer = scorer;
        _promptBuilder = promptBuilder;
    }

    public async Task<AskResult> SummarizeAsync(Guid documentId, ModelPreference preference, CancellationToken ct)
    {
        var text = await ReadyDocumentText.LoadAsync(_repository, documentId, ct);
        if (!text.IsSuccess)
        {
            return AskResult.Failure(text.FailureKind, text.Error);
        }

        if (text.Value.Trim().Length < MinimumCharacters)
        {
            return AskResult.Failure(AskFailureKind.NotEnoughText, TooLittleText);
        }

        var route = Route(text.Value, text.IsPrivate, preference);
        return route.IsSuccess
            ? await SummariseAsync(route.Provider, route.Backend.Value, text.Value, ct)
            : AskFailureMapper.FromRouting(route.FailureKind.Value, route.Error);
    }

    // A summary is scored like a question with no words and no earlier exchanges: only a long reading can count.
    private RoutingResult Route(string documentText, bool isPrivate, ModelPreference preference)
    {
        var score = _scorer.Score(string.Empty, documentText.Length, 0);
        return _router.Route(new RoutingRequest(isPrivate, preference, score.Value));
    }

    private async Task<AskResult> SummariseAsync(IAIProvider provider, ModelBackend backend, string documentText, CancellationToken ct)
    {
        var prompt = _promptBuilder.Build(documentText);
        var answer = await provider.CompleteAsync(prompt.Request, ct);
        if (!answer.IsSuccess)
        {
            return AskFailureMapper.FromProvider(answer.FailureKind.Value, answer.Error);
        }

        // A model that says nothing must never be shown as an empty summary.
        return string.IsNullOrWhiteSpace(answer.Text)
            ? AskResult.Failure(AskFailureKind.Internal, NotProduced)
            : AskResult.Success(answer.Text, prompt.Truncated, backend);
    }
}
