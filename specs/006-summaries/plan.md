# Implementation Plan: Summaries

**Branch**: `008-summaries` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-summaries/spec.md`

## Summary

A person can ask for a summary of a **ready** document: one short paragraph of three to five sentences, written from the first part
of the text (the same part the questions read, with the same "only the first part was read" notice), produced again each time and
**never stored**. A summary goes through exactly the same routing as a question: the same `ModelRouter`, the same preference
(Auto, Local, Cloud), the same refusals (`409` Cloud on a private document, `503` Cloud while off or without a key), no fallback
to the local model, and the same label of which backend answered.

The work is small because almost everything exists. A new Core use case, `DocumentSummaryService`, does what
`DocumentQuestionService` does for a question, with a different prompt: load the ready document, check it has enough text, score,
route, ask the chosen provider. The parts the two services share (loading a ready document, turning routing and provider failures
into the failure kinds the endpoints map) are **moved out into two small helpers** (`ReadyDocumentText` and `AskFailureMapper`) instead of being copied, and
`DocumentQuestionService` calls them; its public shape and its tests do not change. A new `SummaryPromptBuilder` writes the prompt.
A new endpoint `POST /api/documents/{id}/summary` takes an optional `preference` and answers with `summary`, `truncated` and
`backend`. The question panel gets a **Summarise** button that uses the panel's existing **Answer with** selector.

How Auto treats a summary (decided with the user, see [research.md](./research.md) R2): the scorer is reused as it is, with an
empty question and no earlier exchanges. Only one of its five signals can fire, so the score is 0 or 1 and, with the default
threshold of 2, Auto always answers a summary locally. It follows the threshold the person already configures; no new rule and no
new setting.

No stored data and no migration. **No new NuGet package and no new npm package.** The cloud stays off and nothing in this feature
calls the real cloud service.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 5.6 / Angular 19 (frontend, standalone components and signals)

**Primary Dependencies**: None new. Existing: ASP.NET Core minimal API, `Scriptorium.Core`, `OllamaSharp`, the `Anthropic` package
already behind `ClaudeProvider`, Angular `HttpClient`.

**Storage**: None added. No schema change, no migration. The summary exists only in the response and on the page.

**Testing**: Backend: xUnit + FluentAssertions + Moq. New Core test classes: `DocumentSummaryService`, `SummaryPromptBuilder`,
`ReadyDocumentText`, `AskFailureMapper`; extended: `AskResult` and the DTOs. Infrastructure: a new `SummaryEndpointTests` (same
in-process host as `QuestionEndpointTests`, mocked local provider, cloud-on cases with a stand-in `ICloudAIProvider`). The existing
`DocumentQuestionServiceTests` and `QuestionEndpointTests` keep their assertions and their set-up. Frontend: Jasmine/Karma specs for
the ask panel and the API service. The real browser behaviour is checked by hand with [quickstart.md](./quickstart.md). **No test
reaches the real cloud service and none needs a key.**

**Target Platform**: Local machine (macOS), browser; the local model runs in Ollama.

**Project Type**: Web application (ASP.NET Core API + Angular), Clean Architecture.

**Performance Goals**: The same as a question: the local model decides the time; the existing `Ollama:TimeoutSeconds` (120)
applies. The text sent is cut at `Qa:MaxDocumentCharacters` (8000), so a summary never reads more than a question does.

**Constraints**: One short paragraph; first part of the text only; nothing stored; private documents never reach the cloud; no
fallback when the cloud fails; no new package; every method under 30 lines.

**Scale/Scope**: One document at a time, one user.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | How this plan meets it |
|---|---|
| I. Clean Architecture | The use case, the prompt builder and the helpers are in Core and depend only on Core interfaces (`IDocumentRepository`, `IAIProvider`, `ICloudAIProvider` through the router). The endpoint only parses the request and maps a result to a status |
| II. Async-First | `DocumentSummaryService.SummarizeAsync` is `async` and passes the `CancellationToken` down; no `.Result` or `.Wait()` |
| III. Nullable Safety | No `!`; the one place that needs the compiler's help keeps its existing `MemberNotNullWhen` pattern |
| IV. Result pattern | A summary returns `AskResult` (success with text, truncation and backend, or a failure kind and a message); no exception crosses a layer |
| V. Constructor injection | `DocumentSummaryService(repository, router, scorer, promptBuilder)`; the helpers take what they need as arguments (`AskFailureMapper` is static, `ReadyDocumentText` has a static `LoadAsync` and is its own result) |
| VI. Single responsibility, under 30 lines | One class per job: use case, prompt, loading a ready document, mapping failures. `DocumentQuestionService` gets **smaller** (its loader and its two mappers move out) |
| VII. Provider isolation | `ModelRouter` stays the only place that chooses a backend; the summary asks it and uses the provider it hands back. No `HttpClient` and no Ollama or cloud code is added; no provider class changes |
| VIII. Test parity | Every new production class has a test class (see Testing); the helpers extracted from `DocumentQuestionService` get their own, and its existing tests stay as they are |
| No new package | None added to any `.csproj` or `package.json` |
| Never commit secrets | No key, no setting with a secret; `Anthropic:ApiKey` is untouched and the cloud stays off |
| Interface before implementation | No new interface is needed: the use case is a concrete service like the other services in Core |

No violation. **Complexity Tracking** is empty.

## Points for the user

1. **Decided with the user (2026-11-16), how Auto scores a summary:** the existing scorer, reused with an empty question and no
   earlier exchanges. Consequence: with the default threshold Auto always answers a summary locally; a summary goes to the cloud
   under Auto only if the person lowers `Routing:ComplexityThreshold` (and then only for a long document). Choosing Cloud by hand
   works as for questions.
2. **Where the button is (my choice, say if you prefer another):** a **Summarise** button inside the question panel, next to the
   **Answer with** selector, so the summary uses the same selector, the same reset on opening another document and the same label.
   The summary appears above the conversation and is **not** part of the history sent with the questions.
3. **"Too little text" (my choice):** a document with fewer than **50 characters** of text (after trimming) is not sent to the model
   and gets `409` with `The document has too little text to summarise.`. The 50 is a constant with tests, not a setting.
4. **Status for that case:** `409`, because it is a state of the document (like "not ready"), so the page needs no new message table.

## Project Structure

### Documentation (this feature)

```text
specs/006-summaries/
├── plan.md              # This file
├── research.md          # Phase 0: decisions and alternatives
├── data-model.md        # Phase 1: the summary and the request/response shapes
├── quickstart.md        # Phase 1: how to validate by hand and by test
├── contracts/
│   └── summary-api.md   # Phase 1: POST /api/documents/{id}/summary
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Ai/SummaryPrompt.cs                  # NEW: the request for the model + the truncated flag
│   ├── Dtos/SummaryDtos.cs                  # NEW: SummaryRequestDto, SummaryResponseDto
│   ├── Enums/AskFailureKind.cs              # + NotEnoughText (added at the end)
│   └── Services/
│       ├── DocumentSummaryService.cs        # NEW: the use case "summarise a document"
│       ├── SummaryPromptBuilder.cs          # NEW: the instructions and the fenced document
│       ├── ReadyDocumentText.cs             # NEW (moved out of DocumentQuestionService): load a ready document and its text
│       ├── AskFailureMapper.cs              # NEW (moved out of DocumentQuestionService): routing and provider failures -> AskResult
│       ├── PromptDocumentBlock.cs           # NEW (moved out of QuestionPromptBuilder): the fenced <document> block
│       ├── DocumentQuestionService.cs       # uses the two helpers; same public shape
│       └── QuestionPromptBuilder.cs         # uses PromptDocumentBlock; same output
├── Scriptorium.API/
│   ├── Endpoints/SummaryEndpoints.cs        # NEW: POST /api/documents/{id}/summary
│   ├── Endpoints/ModelPreferenceParser.cs   # NEW (moved out of QuestionEndpoints): "auto|local|cloud" -> ModelPreference
│   ├── Endpoints/AskFailureResponses.cs     # NEW (moved out of QuestionEndpoints): failure kind -> HTTP status
│   ├── Endpoints/QuestionEndpoints.cs       # uses the two helpers; same behaviour
│   └── Program.cs                           # registers DocumentSummaryService, SummaryPromptBuilder and maps the endpoint
└── Scriptorium.Web/src/
    ├── app/documents/document.models.ts             # + SummaryResponse
    ├── app/documents/document-api.service.ts        # + summarize(id, preference)
    ├── app/documents/ask/ask-panel.component.ts     # + summary state, Summarise button handler
    ├── app/documents/ask/ask-panel.component.html   # + button and the summary card with label and notice
    └── styles.css                                   # + a few styles in the existing style

tests/
├── Scriptorium.Core.Tests/
│   ├── Services/DocumentSummaryServiceTests.cs      # NEW
│   ├── Services/SummaryPromptBuilderTests.cs        # NEW
│   ├── Services/ReadyDocumentTextTests.cs           # NEW
│   ├── Services/AskFailureMapperTests.cs            # NEW
│   ├── Services/PromptDocumentBlockTests.cs         # NEW
│   └── Dtos/SummaryDtosTests.cs                     # NEW
└── Scriptorium.Infrastructure.Tests/
    └── Endpoints/SummaryEndpointTests.cs            # NEW (mocked local provider, stand-in cloud)
```

**Structure Decision**: the existing four-project layout. Everything the summary needs to decide lives in Core; the API and the
page only carry the request and show the result. The helpers are extracted so the two use cases do not copy each other.

## Suggested order for the tasks

1. **Foundation (no behaviour change, existing tests stay green):** extract `ReadyDocumentText`, `AskFailureMapper` and
   `PromptDocumentBlock` from `DocumentQuestionService` and `QuestionPromptBuilder`, each with its tests written first; extract
   `ModelPreferenceParser` and `AskFailureResponses` from `QuestionEndpoints`. Run all tests: nothing existing changes.
2. **Use case (tests first):** `AskFailureKind.NotEnoughText`, `SummaryPrompt`, `SummaryPromptBuilder`, `DocumentSummaryService`.
3. **API and wiring:** the DTOs, `SummaryEndpoints`, `Program.cs`, the endpoint tests including the stand-in cloud cases.
4. **Page:** the model and the service method, the panel button and the summary card, the specs, the styles.
5. **Docs and final run:** README, `docs/architecture.md`, the constitution review, the full test run, `validation-results.md`.

Manual checks with the user (never committed before the user's ok): after the use case and endpoint (scenarios 1 to 4 by
`curl`), after the page (scenarios 5 to 7), and the cloud-on-without-key check (scenario 8).

## Complexity Tracking

*Nothing to justify.*
