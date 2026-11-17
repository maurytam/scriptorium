using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>The use case "summarise a ready document". The summary is produced each time and never stored.</summary>
public sealed class DocumentSummaryService
{
    private const string TooLittleText = "The document has too little text to summarise.";

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
        var routed = await RoutedDocument.PrepareAsync(_repository, _router, _scorer, documentId, preference, TooLittleText, ct);
        if (!routed.IsSuccess)
        {
            return routed.Failure;
        }

        var prompt = _promptBuilder.Build(routed.Text);
        return await routed.CompleteAsync(prompt.Request, prompt.Truncated, ct);
    }
}
