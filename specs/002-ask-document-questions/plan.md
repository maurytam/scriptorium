# Implementation Plan: Ask Questions About a Document

**Branch**: `002-ask-document-questions` | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-ask-document-questions/spec.md`

## Summary

A user opens a "ready" document from the list, asks a question in natural language and reads an answer based
on the document's extracted text, then can ask follow-ups in the same conversation. The answer is produced by
a model running on the user's own machine (Ollama), so no document text or question ever leaves the computer,
whether or not the document is private. The server is stateless: the browser keeps the conversation and sends
the earlier exchanges with each question, so nothing is stored (FR-007). Documents longer than the model's window
are cut at the end and the user is told. This is the first AI feature, so it also introduces the
`IAIProvider` abstraction that the later "Model router" feature will build on; here there is a single
implementation, `OllamaProvider`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 5.6 / Angular 19 (frontend)

**Primary Dependencies**: ASP.NET Core 10 minimal API; **OllamaSharp 5.5.0** (new package, approved by the user on
2026-10-01, referenced only by `Scriptorium.Infrastructure`); existing Angular `HttpClient`. No other new package.

**Storage**: None added. The feature reads `Document` and `ExtractedText` (feature 001) and persists nothing; no
migration (see [data-model.md](./data-model.md)).

**Testing**: xUnit + FluentAssertions + Moq; `WebApplicationFactory` integration tests with a fake `IAIProvider`;
`OllamaProvider` against a mocked `IOllamaApiClient`; Karma/Jasmine specs for Angular; the quality properties that
need the real model (speed, unanswerable questions, follow-ups, no outbound traffic) are validated manually through
[quickstart.md](./quickstart.md) with Ollama 0.35 and `qwen3.5:9b`.

**Target Platform**: Local macOS web app; Ollama running on the same machine (checked: version 0.35.0, model
`qwen3.5:9b` installed)

**Project Type**: Web application (Angular frontend + ASP.NET Core backend), extending the existing solution

**Performance Goals**: A visible "working" indicator within 1 second and the answer within 60 seconds for 90% of
questions on a typical document (SC-001); the call to the model gives up after 120 seconds (configurable)

**Constraints**: Nothing leaves the user's computer (FR-004, SC-002); one question at a time (FR-005); working window
of 8,192 tokens and a document budget of 4,000 characters, both configurable (research R5, R13); the server keeps no
conversation state; the rest of the app stays usable while an answer is prepared (FR-011)

**Scale/Scope**: Single local user; one document per conversation; at most 2,000 characters per question and the 10
most recent exchanges as context

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Clean Architecture | `IAIProvider` and every type it uses live in `Scriptorium.Core`; OllamaSharp and all Ollama knowledge stay in `Scriptorium.Infrastructure`; Core has no reference to either | PASS |
| II. Async-First | Question service, provider and endpoint are `async`/`await` end to end with the request's cancellation token; no `.Result`/`.Wait()` | PASS |
| III. Nullable Reference Safety | New code in projects with nullable enabled; no `!` without a comment | PASS |
| IV. Result<T> | Failures travel as `AiResult` and `AskResult` with typed kinds (same pattern as `UploadResult`); exceptions are used only for cancellation | PASS |
| V. Constructor Injection | Service, builder and provider receive their dependencies by constructor; composition stays in `Program.cs` | PASS |
| VI. Single Responsibility & Simplicity | Prompt building, the question use case, the Ollama call and the HTTP mapping are four separate small classes; methods stay under 30 lines | PASS |
| VII. Provider Isolation | Ollama logic only in `OllamaProvider`; Core depends on `IAIProvider` alone, so adding Claude and `ModelRouter` later changes no existing class; no `HttpClient` use outside the provider layer | PASS |
| VIII. Test Parity | Every new production class gets a test class (listed in Project Structure) | PASS |
| Non-negotiable: no new NuGet package without approval | OllamaSharp 5.5.0 approved by the user on 2026-10-01 | PASS |
| Non-negotiable: secrets | No secret involved; the Ollama settings are plain configuration | PASS |

**Post-design re-check** (after research, data model and contracts): no change, all gates still pass.
Note on Principle VII: the `ModelRouter` is deliberately not part of this feature; the privacy rule is met here
because there is no cloud provider at all (spec Assumptions).

## Project Structure

### Documentation (this feature)

```text
specs/002-ask-document-questions/
├── plan.md                  # This file
├── research.md              # Phase 0: decisions and rationale
├── data-model.md            # Phase 1: transient types and validation rules (no persistence)
├── quickstart.md            # Phase 1: validation scenarios against the real model
├── contracts/
│   ├── ask-api.md           # POST /api/documents/{id}/ask
│   └── ai-provider.md       # internal IAIProvider contract
├── checklists/
│   └── requirements.md      # spec quality checklist
└── tasks.md                 # Phase 2 output (/speckit-tasks) - NOT created by /speckit-plan
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Ai/
│   │   ├── AiMessage.cs            # record: role + content
│   │   ├── AiRequest.cs            # record: ordered messages
│   │   ├── ChatExchange.cs         # record: one earlier question/answer
│   │   └── QaOptions.cs            # record: limits (question, document, history)
│   ├── Enums/
│   │   ├── AiRole.cs
│   │   ├── AiFailureKind.cs
│   │   └── AskFailureKind.cs
│   ├── Interfaces/
│   │   └── IAIProvider.cs
│   ├── Results/
│   │   ├── AiResult.cs
│   │   └── AskResult.cs
│   ├── Services/
│   │   ├── QuestionPromptBuilder.cs
│   │   └── DocumentQuestionService.cs
│   └── Dtos/
│       └── AskDtos.cs              # AskRequestDto, ChatExchangeDto, AskResponseDto
│
├── Scriptorium.Infrastructure/
│   └── Ai/
│       ├── OllamaProvider.cs       # the only class that knows Ollama / OllamaSharp
│       └── OllamaOptions.cs        # record: base URL, model, window, timeout
│
├── Scriptorium.API/
│   ├── Endpoints/
│   │   └── QuestionEndpoints.cs    # POST /api/documents/{id}/ask and the error mapping
│   ├── Program.cs                  # registrations, options binding (changed)
│   └── appsettings.json            # Ollama and Qa keys (changed)
│
└── Scriptorium.Web/src/
    ├── styles.css                  # conversation card styling (changed)
    └── app/
        ├── app.component.html      # adds the ask panel (changed)
        └── documents/
            ├── document.models.ts          # ask types (changed)
            ├── document-api.service.ts     # ask() (changed)
            ├── ask/
            │   ├── ask-panel.component.{ts,html,spec.ts}      # conversation card
            │   └── conversation-selection.service.{ts,spec.ts} # which document is open
            └── document-list/              # "Ask" button on ready rows (changed)

tests/
├── Scriptorium.Core.Tests/
│   ├── Services/QuestionPromptBuilderTests.cs
│   ├── Services/DocumentQuestionServiceTests.cs
│   ├── Results/AiResultTests.cs
│   ├── Results/AskResultTests.cs
│   └── Dtos/AskDtosTests.cs
└── Scriptorium.Infrastructure.Tests/
    ├── Ai/OllamaProviderTests.cs
    └── Endpoints/QuestionEndpointTests.cs   # WebApplicationFactory + fake IAIProvider
```

**Structure Decision**: The existing four-project layout is kept. The AI abstraction is new Core surface
(`Ai/`, `IAIProvider`), its only implementation is a new `Infrastructure/Ai/` folder, and the HTTP mapping goes
in a separate `QuestionEndpoints` class instead of growing `DocumentEndpoints`. On the frontend the conversation
card is a new component beside the list, joined to it by a small selection service rather than a router.
`QaOptions` and `OllamaOptions` are logic-free records and are tested through the classes that use them.

## Complexity Tracking

*No Constitution Check violations. The single new dependency (OllamaSharp) was approved explicitly and is the
documented stack in `CLAUDE.md`, so it is not a deviation; this table is intentionally empty.*
