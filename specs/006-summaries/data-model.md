# Data Model: Summaries

**No stored data and no migration.** A summary is produced from existing data and sent back; it is not kept anywhere.

## The summary (transient)

What one request produces. It exists only in the response and on the page.

| Field | Meaning |
|---|---|
| `Summary` | The paragraph: three to five sentences, plain text, in the language of the document |
| `Truncated` | True only when the document text is longer than `Qa:MaxDocumentCharacters`, so the summary is based on the first part |
| `Backend` | `local` (Ollama, on this computer) or `cloud`; always `local` with the delivered settings |

In Core it travels as an `AskResult` (success: text, truncated, backend), the type the question flow already uses, so the same
failure kinds and the same mapping apply. The response DTO names the text `summary` instead of `answer`.

## Failure kinds

`AskFailureKind` gains one value, **`NotEnoughText`**, added **at the end** so the existing values keep their numbers. Everything
else a summary can fail with already exists:

| Kind | When | HTTP |
|---|---|---|
| `InvalidQuestion` | not used by a summary | — |
| `NotFound` | no such document | 404 |
| `NotReady` | document processing or failed | 409 |
| **`NotEnoughText`** | trimmed text under 50 characters | 409 |
| `PreferenceNotAllowed` | Cloud chosen for a private document | 409 |
| `ModelUnavailable` | Cloud chosen while off or without a key, or the model not reachable | 503 |
| `Timeout` | no answer in time | 504 |
| `Internal` | anything else | 500 |

## Request and response shapes

```text
SummaryRequestDto   ( string? Preference = null )                   // optional body; null or empty = Auto
SummaryResponseDto  ( string Summary, bool Truncated, string Backend = "local" )
SummaryPrompt       ( AiRequest Request, bool Truncated )            // Core, between the prompt builder and the service
```

## The router and the scorer

Unchanged. `DocumentSummaryService` asks `ComplexityScorer.Score("", text.Length, 0)` and `ModelRouter.Route(new RoutingRequest(
isPrivate, preference, score))`. The decision table of feature 005 applies as it is; for a summary the score column is only ever
0 or 1 (research R2).

## Constants

| Constant | Value | Where | Why |
|---|---|---|---|
| Minimum text | 50 characters (trimmed) | `DocumentSummaryService` | below this there is nothing to summarise (R4) |
| Length asked of the model | 3 to 5 sentences, one paragraph | `SummaryPromptBuilder` | the user's choice (spec FR-005) |
| Characters read | `Qa:MaxDocumentCharacters` (8000) | existing setting | the same part the questions read |

No new setting.
