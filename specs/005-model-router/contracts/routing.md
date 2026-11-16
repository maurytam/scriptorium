# Internal Contract: routing, scoring and the cloud provider

How the pieces meet. They are in `Scriptorium.Core` unless stated otherwise; Core references nothing in Infrastructure.

## `ICloudAIProvider` (new interface, written before the implementation)

```csharp
public interface ICloudAIProvider : IAIProvider
{
    /// <summary>True only when the provider has what it needs to call the service (a key).</summary>
    bool IsConfigured { get; }
}
```

- `CompleteAsync(AiRequest, CancellationToken)` is inherited from `IAIProvider` with the **same promises**: an expected problem
  comes back as a failed `AiResult` (`Unavailable`, `Timeout`, `Failed`); cancellation by the caller propagates as
  `OperationCanceledException`.
- When `IsConfigured` is false, `CompleteAsync` returns `Unavailable` **without sending anything**.
- Its wording is its own ("cloud model"); `DocumentQuestionService` passes the text of `Unavailable` and `Timeout` through, as it
  does for Ollama.
- Implemented only by `ClaudeProvider` in `Scriptorium.Infrastructure`. `IAIProvider` itself is still implemented only by
  `OllamaProvider` and still registered as the local provider.

## `ComplexityScorer`

```csharp
public ComplexityScore Score(string question, int documentLength, int earlierExchanges)
```

- **Inputs are a text and two numbers.** The scorer never sees the document.
- Pure and deterministic: the same arguments give the same score. No I/O, no clock, no randomness.
- `question` is expected already trimmed (the service trims it); the scorer does not change it.
- Returns the signals that fired; `Value` is how many (0 to 5). The limits and the word list are the constants of
  [research.md](../research.md) R3 and [data-model.md](../data-model.md).

## `ModelRouter`

```csharp
public RoutingResult Route(RoutingRequest request)
```

- Constructed with the local `IAIProvider`, the `ICloudAIProvider` and the `RoutingOptions`.
- Applies the rules of [research.md](../research.md) R1 in order and returns the backend with its provider, or a failure kind.
- **The only place** that decides between local and cloud. Compares the score with `ComplexityThreshold` (above = complex); it
  does not compute it.
- Never calls a provider; it only hands one back. So a refusal can never have contacted a model.

## `DocumentQuestionService` (changed)

Flow after the change: validate the question → load the document (now also its `IsPrivate`) → score → route → on a routing
failure, return the failure (no model called) → build the prompt → call **the provider the router returned** → return the answer
with the backend.

- `AskAsync(Guid documentId, string question, IReadOnlyList<ChatExchange>? history, ModelPreference preference, CancellationToken ct)`
- The score uses `question.Trim().Length` and the like on the trimmed question, `text.Length` as the document length, and
  `history?.Count ?? 0`.
- Routing failure → `AskResult.Failure`: `CloudNotAvailable` → `ModelUnavailable` (`503`), `PrivateDocumentCloudRefused` →
  `PreferenceNotAllowed` (`409`).

## Configuration

| Key | Type | Default | Where |
|---|---|---|---|
| `Routing:CloudEnabled` | bool | `false` | `appsettings.json` |
| `Routing:ComplexityThreshold` | int | `2` | `appsettings.json` |
| `Anthropic:ApiKey` | string | none | `dotnet user-secrets` or environment only |
| `Anthropic:Model` | string | `claude-sonnet-4-20250514` | `appsettings.json` |
| `Anthropic:TimeoutSeconds` | int | `120` | `appsettings.json` |
| `Anthropic:MaxTokens` | int | `1024` | `appsettings.json` |

Any key can be an environment variable (`Routing__CloudEnabled`). No secret is ever written to a committed file.
