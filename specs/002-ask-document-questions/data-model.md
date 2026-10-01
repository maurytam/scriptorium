# Phase 1 Data Model: Ask Questions About a Document

**This feature adds no persistent data and no database migration.** A conversation is never stored
(FR-007): it exists only in the browser while the page is open and is carried to the server inside each
request. The existing `Document` and `ExtractedText` entities (feature 001) are read, never changed.

## Types in `Scriptorium.Core` (transient, not persisted)

### AI abstraction

| Type | Kind | Notes |
|---|---|---|
| `IAIProvider` | interface | `Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)`; implemented by `OllamaProvider` in `Infrastructure` |
| `AiRole` | enum | `System`, `User`, `Assistant` |
| `AiMessage` | record | `Role`, `Content` |
| `AiRequest` | record | Ordered list of `AiMessage` |
| `AiFailureKind` | enum | `Unavailable`, `Timeout`, `Failed` |
| `AiResult` | result | Success carries the answer text; failure carries `AiFailureKind` and a message (same shape as `UploadResult`) |

### Asking a question

| Type | Kind | Notes |
|---|---|---|
| `ChatExchange` | record | `Question`, `Answer`: one earlier turn of the conversation |
| `QaOptions` | record | `MaxQuestionLength`, `MaxDocumentCharacters`, `MaxHistoryExchanges`, `MaxHistoryCharacters` (see research R12) |
| `QuestionPromptBuilder` | class | Builds the `AiRequest` and reports whether the document was truncated |
| `AskFailureKind` | enum | `InvalidQuestion`, `NotFound`, `NotReady`, `ModelUnavailable`, `Timeout`, `Internal` |
| `AskResult` | result | Success: `Answer` and `Truncated`; failure: `AskFailureKind` and a message |
| `DocumentQuestionService` | service | Validates, loads the document and its text, builds the prompt, calls `IAIProvider`, maps the outcome |

### DTOs (`Scriptorium.Core.Dtos`)

| Type | Fields |
|---|---|
| `AskRequestDto` | `Question`, `History` (list of `ChatExchangeDto`) |
| `ChatExchangeDto` | `Question`, `Answer` |
| `AskResponseDto` | `Answer`, `Truncated` |

## Validation rules

- **Question**: trimmed; must not be empty; at most `Qa:MaxQuestionLength` characters (default 2,000).
  Violations → `InvalidQuestion` (FR-010).
- **History**: optional (a first question has none). Only the most recent `Qa:MaxHistoryExchanges`
  exchanges, and no more than `Qa:MaxHistoryCharacters` characters in total, are passed to the model;
  older ones are dropped silently (the screen still shows them).
- **Document**: must exist (`NotFound`) and have status `Ready` (`NotReady`, naming the status) (FR-002).
- **Document text**: used up to `Qa:MaxDocumentCharacters` characters; beyond that it is cut at the end and
  `Truncated` is set (FR-008).

## Conversation state (browser only)

```text
(no document chosen) -> Open(document) -> Idle
Idle  --send question--> Waiting --answer--> Idle   (answer appended to the transcript)
Waiting --failure--> Idle                            (error shown, question kept for retry)
Open / Close / choose another document / document deleted / page reload -> transcript discarded
```

`Waiting` is the only state in which the send control is disabled (one question at a time, FR-005).

## Configuration records (`Infrastructure` and API, no logic)

`OllamaOptions` (`BaseUrl`, `Model`, `NumCtx`, `TimeoutSeconds`) and `QaOptions` are bound from
`appsettings.json` (research R12). They hold no behaviour and are covered through the classes that use them.
