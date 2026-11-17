# Implementation Plan: Entity Extraction

**Branch**: `009-entity-extraction` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-entity-extraction/spec.md`

## Summary

A person can ask for the **key entities** of a **ready** document: the people, organizations, dates, amounts of money and places
that its text mentions, grouped by type, each shown as written, once, in the order it first appears. Only entities that really
appear in the part of the text that was read are ever shown. The result is produced again each time and **never stored**. It goes
through exactly the same routing as a question and a summary: the same `ModelRouter`, the same preference (Auto, Local, Cloud),
the same refusals (`409` Cloud on a private document, `503` Cloud while off or without a key), no fallback to the local model, the
same label of which backend answered and the same "only the first part was read" notice.

The work is moderate because the path already exists. The model is asked for **one entity per line** (`kind: text`), which the
code reads and then **checks against the text** (the "nothing is invented" rule), removes the repeats from and puts in order.
Three things are new in Core: `EntityPromptBuilder` (the prompt), `EntityReplyParser` and `EntityVerifier` (read the reply, keep
only what is in the text), and `DocumentEntityService` (the use case). A new endpoint `POST /api/documents/{id}/entities` takes an
optional `preference` and answers with the groups, `truncated` and `backend`. The question panel gets an **Extract entities**
button next to **Summarise**, using the panel's existing **Answer with** selector.

What a summary and an extraction have in common (load the ready document, refuse a text that is too short, score, route, call the
provider, map its failure, refuse an empty reply) is **moved out of `DocumentSummaryService` into one small piece,
`RoutedDocument`**, which both services use, instead of being copied (research R1). `DocumentSummaryService` keeps its
constructor, its public method, its messages and its tests unchanged. This is point 1 of "Points for the user".

How Auto treats an extraction: as for a summary, the scorer is reused with an empty question and no earlier exchanges, so the score
is 0 or 1 and, with the default threshold of 2, Auto always answers locally. No new rule and no new setting.

No stored data and no migration. **No new NuGet package and no new npm package.** No change to `AiRequest`, to `IAIProvider` or to
either provider. The cloud stays off and nothing in this feature calls the real cloud service.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 5.6 / Angular 19 (frontend, standalone components and signals)

**Primary Dependencies**: None new. Existing: ASP.NET Core minimal API, `Scriptorium.Core`, `OllamaSharp`, the `Anthropic` package
already behind `ClaudeProvider`, Angular `HttpClient`.

**Storage**: None added. No schema change, no migration. The entities exist only in the response and on the page.

**Testing**: Backend: xUnit + FluentAssertions + Moq. New Core test classes: `RoutedDocument`, `EntityPromptBuilder`,
`EntityReplyParser`, `EntityVerifier`, `DocumentEntityService`, `EntityResult`, the DTOs. Infrastructure: a new
`EntityEndpointTests` (same in-process host as `SummaryEndpointTests`, mocked local provider, cloud-on cases with a stand-in
`ICloudAIProvider`). The existing `DocumentSummaryServiceTests`, `SummaryEndpointTests`, `DocumentQuestionServiceTests` and
`QuestionEndpointTests` keep their assertions and their set-up. Frontend: Jasmine/Karma specs for the ask panel and the API
service. The real browser behaviour is checked by hand with [quickstart.md](./quickstart.md). **No test reaches the real cloud
service and none needs a key.**

**Target Platform**: Local machine (macOS), browser; the local model runs in Ollama.

**Project Type**: Web application (ASP.NET Core API + Angular), Clean Architecture.

**Performance Goals**: The same as a summary: the local model decides the time; the existing `Ollama:TimeoutSeconds` (120)
applies. The text sent is cut at `Qa:MaxDocumentCharacters` (8000), so an extraction never reads more than a question does. The
check of the entities against the text is a handful of string searches over at most 8,000 characters: not measurable next to the
model.

**Constraints**: Five fixed types; only entities found in the text read; first part of the text only; nothing stored; private
documents never reach the cloud; no fallback when the cloud fails; no new package; no change to the providers; every method under
30 lines.

**Scale/Scope**: One document at a time, one user.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | How this plan meets it |
|---|---|
| I. Clean Architecture | The use case, the prompt builder, the parser, the verifier and `RoutedDocument` are in Core and depend only on Core interfaces (`IDocumentRepository`, `IAIProvider`, `ICloudAIProvider` through the router). The endpoint only parses the request and maps a result to a status |
| II. Async-First | `DocumentEntityService.ExtractAsync` and `RoutedDocument.PrepareAsync`/`CompleteAsync` are `async` and pass the `CancellationToken` down; no `.Result` or `.Wait()` |
| III. Nullable Safety | No `!`; the places that need the compiler's help keep the existing `MemberNotNullWhen` pattern |
| IV. Result pattern | An extraction returns `EntityResult` (success with groups, truncation and backend, or a failure kind and a message), a step before it returns `AskResult`; no exception crosses a layer. The reading of the reply is a pure function that reports "unreadable" as a value |
| V. Constructor injection | `DocumentEntityService(repository, router, scorer, promptBuilder)`, the same shape as the summary service; `RoutedDocument` and the parser/verifier are static helpers that take what they need as arguments, as `ReadyDocumentText` does |
| VI. Single responsibility, under 30 lines | One class per job: use case, prompt, reading the reply, checking the entities, preparing a routed document, the result. `DocumentSummaryService` gets **smaller** (its checks, scoring, routing and provider call move out) |
| VII. Provider isolation | `ModelRouter` stays the only place that chooses a backend; `RoutedDocument` asks it and uses the provider it hands back. No `HttpClient` and no Ollama or cloud code is added; no provider class changes, and `AiRequest` is untouched |
| VIII. Test parity | Every new production class has a test class (see Testing); `RoutedDocument` gets its own, and the summary tests stay as they are as the safety net of the move |
| No new package | None added to any `.csproj` or `package.json` |
| Never commit secrets | No key, no setting with a secret; `Anthropic:ApiKey` is untouched and the cloud stays off |
| Interface before implementation | No new interface is needed: the use case is a concrete service like the other services in Core |

No violation. **Complexity Tracking** is empty.

## Points for the user

**Confirmed by the user on 2026-11-17: all five points are accepted as written.**

1. **Moving the shared half out of `DocumentSummaryService` (my choice, say if you prefer another).** Recommended: the checks,
   the scoring, the routing and the provider call that a summary and an extraction share go into `RoutedDocument`, and the
   summary service uses it. The summary tests, which are strict about the order of the checks, are not edited and prove the move.
   The alternative is to leave the summary service alone and copy about 40 lines into the new service: a smaller diff to review, but
   two copies of rules that include a privacy rule.
2. **One entity per line, not JSON (my choice).** A 4B model breaks JSON often and one broken character loses the whole answer;
   a line format loses only the bad line and needs no change to the providers (research R2).
3. **What "nothing is invented" costs (a consequence of your rule).** An entity is shown only if its text is found in the text that
   was read (apart from letter case and spaces). So a date the model rewrites ("March 12, 2026" for "12 March 2026") is dropped.
   The prompt asks to copy each entity exactly, so this should be rare, but it will happen (research R3).
4. **The new message for a text that is too short:** `The document has too little text to extract entities from.`, with the same
   `409` and the same 50-character minimum as summaries.
5. **No cap on the number of entities.** The text read is at most 8,000 characters, so the list is bounded in practice; the page
   lays the entities out as a wrapping list. If a real document shows this is a problem, a cap is a small change.

## Project Structure

### Documentation (this feature)

```text
specs/007-entity-extraction/
├── plan.md              # This file
├── research.md          # Phase 0: decisions and alternatives
├── data-model.md        # Phase 1: the entities and the request/response shapes
├── quickstart.md        # Phase 1: how to validate by hand and by test
├── contracts/
│   └── entities-api.md  # Phase 1: POST /api/documents/{id}/entities
├── checklists/
│   └── requirements.md  # The specification quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Ai/EntityPrompt.cs                   # NEW: the request for the model + the truncated flag + the text it was given
│   ├── Ai/EntityCandidate.cs                # NEW: one line of the model's reply (type and text), not yet checked
│   ├── Dtos/EntityDtos.cs                   # NEW: EntityRequestDto, EntityGroupDto, EntityResponseDto
│   ├── Enums/EntityType.cs                  # NEW: Person, Organization, Date, Amount, Place
│   ├── Results/EntityResult.cs              # NEW: success (groups, truncated, backend) or failure (kind, message)
│   ├── Results/EntityGroup.cs               # NEW: the checked entities of one type
│   └── Services/
│       ├── RoutedDocument.cs                # NEW (moved out of DocumentSummaryService): load, minimum text, score, route, call the provider
│       ├── DocumentEntityService.cs         # NEW: the use case "extract the entities of a document"
│       ├── EntityPromptBuilder.cs           # NEW: the instructions and the fenced document
│       ├── EntityReplyParser.cs             # NEW: the model's lines -> candidate entities, or "none", or "unreadable"
│       ├── EntityReply.cs                   # NEW: what the parser returns (EntityReplyKind: Entities, None, Unreadable, and the candidates)
│       ├── EntityVerifier.cs                # NEW: keep what is in the text, no repeats, in order of appearance
│       └── DocumentSummaryService.cs        # uses RoutedDocument; same public shape and messages
├── Scriptorium.API/
│   ├── Endpoints/EntityEndpoints.cs         # NEW: POST /api/documents/{id}/entities
│   └── Program.cs                           # registers EntityPromptBuilder, DocumentEntityService and maps the endpoint
└── Scriptorium.Web/src/
    ├── app/documents/document.models.ts             # + EntitiesResponse, EntityGroup
    ├── app/documents/document-api.service.ts        # + extractEntities(id, preference)
    ├── app/documents/ask/ask-panel.component.ts     # + entities state, Extract entities handler; one shared "ask the server" path
    ├── app/documents/ask/ask-panel.component.html   # + button and the entities card with label, notice and "none found"
    └── styles.css                                   # + a few styles in the existing style

tests/
├── Scriptorium.Core.Tests/
│   ├── Services/RoutedDocumentTests.cs              # NEW
│   ├── Services/DocumentEntityServiceTests.cs       # NEW
│   ├── Services/EntityPromptBuilderTests.cs         # NEW
│   ├── Services/EntityReplyParserTests.cs           # NEW
│   ├── Services/EntityVerifierTests.cs              # NEW
│   ├── Results/EntityResultTests.cs                 # NEW
│   └── Dtos/EntityDtosTests.cs                      # NEW
└── Scriptorium.Infrastructure.Tests/
    └── Endpoints/EntityEndpointTests.cs             # NEW (mocked local provider, stand-in cloud)
```

**Structure Decision**: the existing four-project layout. Everything the extraction decides lives in Core; the API and the page
only carry the request and show the result.

## Suggested order for the tasks

1. **Foundation (no behaviour change, existing tests stay green):** `RoutedDocument` with its tests written first, then
   `DocumentSummaryService` changed to use it. Run all tests: nothing existing changes, and **no existing test is edited**.
2. **The pieces of the extraction (tests first):** `EntityType`, `EntityPrompt`, `EntityPromptBuilder`, `EntityReplyParser`,
   `EntityVerifier`, `EntityResult`.
3. **Use case, API and wiring:** `DocumentEntityService`, the DTOs, `EntityEndpoints`, `Program.cs`, the endpoint tests
   (the summary's routing cases again, plus the cases of the reply: none, unreadable, invented entities dropped).
4. **Page:** the model and the service method, the button and the entities card (with the notice for a long document and the
   "none found" message), the specs, the styles.
5. **Docs and final run:** README, `docs/architecture.md`, the constitution review, the full test run, `validation-results.md`.

Manual checks with the user (never committed before the user's ok): after the use case and endpoint (scenarios 1 to 4 by `curl`),
after the page (scenarios 5 to 7), and the cloud-on-without-key check (scenario 8). Fake files only.

## Complexity Tracking

*Nothing to justify.*
