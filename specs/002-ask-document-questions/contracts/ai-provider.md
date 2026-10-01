# Internal Contract: `IAIProvider`

The boundary between the domain and any answering model (Constitution I and VII). Defined in
`Scriptorium.Core`; implemented in `Scriptorium.Infrastructure`. Today there is one implementation,
`OllamaProvider`. The later "Model router" feature adds providers and a router in front of this interface;
`DocumentQuestionService` does not change.

```text
IAIProvider
  Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)

AiRequest   = ordered list of AiMessage(role: System | User | Assistant, content)
AiResult    = success(text) | failure(kind: Unavailable | Timeout | Failed, message)
```

## Rules for every implementation

- Never throw across the boundary for expected problems: return a failure with the right kind.
  Cancellation requested by the caller is the one exception and propagates as `OperationCanceledException`.
- `Unavailable`: the model or its server cannot be reached, or the model is not installed.
- `Timeout`: no complete answer within the provider's configured time.
- `Failed`: any other error from the model.
- Return only the model's answer text; no hidden reasoning, no markup added by the provider.
- Be safe to call from many requests at once.
- Do not log the document text or the question.

## `OllamaProvider` specifics

- Sends one chat request to `Ollama:BaseUrl` with model `Ollama:Model`, the window `Ollama:NumCtx`, thinking
  disabled and a low temperature (research R6).
- Gives up after `Ollama:TimeoutSeconds`.
- The only class that knows about Ollama or its client library.
