using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>The use case "extract the entities of a ready document". The result is produced each time and never stored.</summary>
public sealed class DocumentEntityService
{
    private const string TooLittleText = "The document has too little text to extract entities from.";
    private const string Unreadable = "The entities could not be extracted.";
    private const string NotProduced = "The answer could not be produced.";

    private readonly IDocumentRepository _repository;
    private readonly ModelRouter _router;
    private readonly ComplexityScorer _scorer;
    private readonly EntityPromptBuilder _promptBuilder;

    public DocumentEntityService(
        IDocumentRepository repository, ModelRouter router, ComplexityScorer scorer, EntityPromptBuilder promptBuilder)
    {
        _repository = repository;
        _router = router;
        _scorer = scorer;
        _promptBuilder = promptBuilder;
    }

    public async Task<EntityResult> ExtractAsync(Guid documentId, ModelPreference preference, CancellationToken ct)
    {
        var routed = await RoutedDocument.PrepareAsync(_repository, _router, _scorer, documentId, preference, TooLittleText, ct);
        if (!routed.IsSuccess)
        {
            return Failed(routed.Failure);
        }

        var prompt = _promptBuilder.Build(routed.Text);
        var reply = await routed.CompleteAsync(prompt.Request, prompt.Truncated, ct);
        return reply.IsSuccess ? ReadReply(reply.Answer, reply.Backend, prompt) : Failed(reply);
    }

    // What was said is checked against the text the model was given; what is not in it is dropped.
    private static EntityResult ReadReply(string reply, ModelBackend backend, EntityPrompt prompt)
    {
        var parsed = EntityReplyParser.Parse(reply);
        return parsed.Kind switch
        {
            EntityReplyKind.Unreadable => EntityResult.Failure(AskFailureKind.Internal, Unreadable),
            EntityReplyKind.None => EntityResult.Success([], prompt.Truncated, backend),
            _ => EntityResult.Success(EntityVerifier.Verify(parsed.Candidates, prompt.TextRead), prompt.Truncated, backend)
        };
    }

    private static EntityResult Failed(AskResult failure) =>
        EntityResult.Failure(failure.FailureKind ?? AskFailureKind.Internal, failure.Error ?? NotProduced);
}
