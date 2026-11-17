using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Ai;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>
/// A ready document that has been checked and routed: its text, and the provider the router chose for it. It is its own result,
/// so a refused document carries the failure instead. What a summary and an entity extraction share lives here; the prompt and
/// what is done with the reply stay with each use case.
/// </summary>
public sealed class RoutedDocument
{
    /// <summary>Under this many characters (trimmed) there is nothing worth processing, and a model would invent.</summary>
    public const int MinimumCharacters = 50;

    private const string NotProduced = "The answer could not be produced.";

    private readonly IAIProvider? _provider;
    private readonly ModelBackend _backend;

    private RoutedDocument(string? text, IAIProvider? provider, ModelBackend backend, AskResult? failure)
    {
        Text = text;
        _provider = provider;
        _backend = backend;
        Failure = failure;
    }

    /// <summary>The text of the document, when it can be processed.</summary>
    public string? Text { get; }

    /// <summary>Why the document was refused, when it was.</summary>
    public AskResult? Failure { get; }

    [MemberNotNullWhen(true, nameof(Text))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSuccess => Text is not null;

    /// <summary>
    /// Loads the document and, in this order: refuses one that is not found or not ready, refuses a text that is too short, scores
    /// the text like a question with no words and no earlier exchanges (only a long reading can count, so the score is 0 or 1),
    /// and asks the router. No model is called here.
    /// </summary>
    public static async Task<RoutedDocument> PrepareAsync(
        IDocumentRepository repository, ModelRouter router, ComplexityScorer scorer, Guid documentId,
        ModelPreference preference, string tooLittleTextMessage, CancellationToken ct)
    {
        var text = await ReadyDocumentText.LoadAsync(repository, documentId, ct);
        if (!text.IsSuccess)
        {
            return Refused(AskResult.Failure(text.FailureKind, text.Error));
        }

        if (text.Value.Trim().Length < MinimumCharacters)
        {
            return Refused(AskResult.Failure(AskFailureKind.NotEnoughText, tooLittleTextMessage));
        }

        var score = scorer.Score(string.Empty, text.Value.Length, 0);
        var route = router.Route(new RoutingRequest(text.IsPrivate, preference, score.Value));
        return route.IsSuccess
            ? new RoutedDocument(text.Value, route.Provider, route.Backend.Value, null)
            : Refused(AskFailureMapper.FromRouting(route.FailureKind.Value, route.Error));
    }

    /// <summary>Calls the provider the router chose, and only that one. A blank reply is a failure, never an empty result.</summary>
    public async Task<AskResult> CompleteAsync(AiRequest request, bool truncated, CancellationToken ct)
    {
        if (_provider is null)
        {
            return Failure ?? AskResult.Failure(AskFailureKind.Internal, NotProduced);
        }

        var reply = await _provider.CompleteAsync(request, ct);
        if (!reply.IsSuccess)
        {
            return AskFailureMapper.FromProvider(reply.FailureKind.Value, reply.Error);
        }

        return string.IsNullOrWhiteSpace(reply.Text)
            ? AskResult.Failure(AskFailureKind.Internal, NotProduced)
            : AskResult.Success(reply.Text, truncated, _backend);
    }

    private static RoutedDocument Refused(AskResult failure) => new(null, null, default, failure);
}
