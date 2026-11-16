# Implementation Plan: Model Router (cloud switched off)

**Branch**: `007-model-router` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-model-router/spec.md`

## Summary

Today every question goes to Ollama and the Private flag is stored but unused. This feature puts one routing decision in front of
every question. A Core class `ModelRouter` decides between the local model and a cloud model from four facts: whether the
document is private, the person's preference (Auto, Local or Cloud), a complexity score, and whether the cloud is usable. A
private document is always answered locally, and choosing Cloud for one is refused. A small Core class `ComplexityScorer` gives
the question a score from 0 to 5 (the rule approved by the user: five signals, one point each, complex when **above** the
threshold, default 2). The cloud side is built for real but **switched off** (`Routing:CloudEnabled` is false), so with the
delivered settings every question goes to Ollama and nothing is spent. Choosing Cloud while it is off gives an explicit error and
never falls back to the local model.

The cloud side is a `ClaudeProvider` in Infrastructure, the only class that knows the cloud library, next to `OllamaProvider`;
the library is the official `Anthropic` NuGet package (free, Apache-2.0, approved by the user). It is tested up to its seam and
never called for real. The endpoint accepts an optional `preference` and answers with a `backend`; the question panel gets a
small selector and an "answered on this computer / by the cloud model" label. The page makes no routing decision.

No stored data and no migration. One new NuGet package (approved), no npm package.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 5.6 / Angular 19 (frontend, standalone components and signals)

**Primary Dependencies**: New: `Anthropic` 13.8.0 in `Scriptorium.Infrastructure` only, plus its dependencies
(`Microsoft.Extensions.AI.Abstractions` 10.5.1, `System.Net.ServerSentEvents` 10.0.1, `System.Text.Json` 10.0.6; see
[research.md](./research.md) R4). Existing: ASP.NET Core minimal API, `Scriptorium.Core`, OllamaSharp, Angular `HttpClient`.

**Storage**: None added. No schema change, no migration.

**Testing**: Backend: xUnit + FluentAssertions + Moq. New Core test classes for `ComplexityScorer` and `ModelRouter` (the whole
decision table as theories) and extended ones for `DocumentQuestionService`, `AskResult` and the DTOs; Infrastructure tests for
`ClaudeProvider` (no network: a stand-in or a fake handler) and the endpoint (`QuestionEndpointTests`, same in-process host,
cloud-on cases with a stand-in cloud provider). Frontend: Jasmine/Karma specs for the panel and the API service. The real browser
behaviour is checked by hand with [quickstart.md](./quickstart.md). **No test reaches the real cloud service.**

**Target Platform**: Local macOS web app (unchanged)

**Project Type**: Web application; this feature touches the Core (routing, scoring, one use case), the Infrastructure (one
provider), the API (one endpoint and the wiring) and the Angular question panel

**Performance Goals**: The routing decision and the score add no perceptible time (a text of at most 2,000 characters and a few
numbers, no I/O, no extra model call). With the delivered settings the time to an answer is the one of feature 002.

**Constraints**: Cloud off by default and no key in any committed file (FR-007, FR-015); nothing sent to any cloud service while
off; a private document never reaches the cloud; no silent fallback; the Ollama and the cloud logic never in the same class;
`HttpClient` only inside a provider; the page does not decide; only the approved package is added.

**Scale/Scope**: Backend about 8 new small types (3 enums, `RoutingOptions`, `RoutingRequest`/`RoutingResult`,
`ComplexityScorer`, `ModelRouter`, `ICloudAIProvider`) plus `ClaudeProvider` and `AnthropicOptions`, and edits to
`DocumentQuestionService`, `AskResult`, the DTOs, the endpoint and `Program.cs`; frontend one selector, one label and a few
model/API lines. About 400 lines of production code and more of tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Clean Architecture | Routing, scoring and the use case are in `Scriptorium.Core` and reach providers only through `IAIProvider` and the new `ICloudAIProvider`; the cloud library is referenced only by Infrastructure; the endpoint only maps the request and the result | PASS |
| II. Async-First | `ClaudeProvider.CompleteAsync` and the service are async; no `.Result` or `.Wait()`; the router and scorer are synchronous because they do no I/O | PASS |
| III. Nullable Reference Safety | Result types use `MemberNotNullWhen` like `AskResult`; no `!` without a comment | PASS |
| IV. Result<T> | `RoutingResult` and `AiResult` carry the failures; the endpoint maps kinds to statuses; no exception crosses a layer | PASS |
| V. Constructor Injection | `ModelRouter`, `ComplexityScorer`, `DocumentQuestionService` and `ClaudeProvider` receive their dependencies by constructor and are registered in `Program.cs`; the frontend uses `inject()` | PASS |
| VI. Single Responsibility & Simplicity | Scoring, deciding and answering are three classes; the router only compares and chooses; methods stay under 30 lines (`AskAsync` is split into small steps, as `Route` is split by rule) | PASS |
| VII. Provider Isolation | `ModelRouter` is the only place that chooses; Ollama and the cloud are separate classes; the only `HttpClient` use is inside a provider; the page does not choose | PASS |
| VIII. Test Parity | A test class for every new class: `ComplexityScorerTests`, `ModelRouterTests`, `ClaudeProviderTests`, the options and DTO checks, the service and endpoint tests extended | PASS |
| Workflow: interface before implementation | `ICloudAIProvider` is written before `ClaudeProvider`; the router and the scorer are concrete classes over interfaces, like the existing Core services | PASS |
| Non-negotiable: no new package without approval | One package, `Anthropic`, approved by the user on 2026-11-16 because it is free; its dependencies are listed in research R4; nothing else is added | PASS (approved) |
| Non-negotiable: no secret committed | The key is read only from user-secrets or the environment; `appsettings.json` has no key | PASS |
| Non-negotiable: no business logic in view components | The panel sends the preference and shows `backend`; the decision, the score and the refusals are on the server | PASS |

**Post-design re-check (after Phase 1)**: unchanged, all PASS. No entry in Complexity Tracking.

## Points for the user

Settled with the user on 2026-11-16:

1. **SC-003 wording.** Adjusted in the spec: no existing test case is removed or has its expectations changed. The set-up line
   and the calls in `DocumentQuestionServiceTests` (about 15 call sites) change mechanically because the constructor and
   `AskAsync` change (research R8).
2. **"perchè".** Added to the fixed list, which now has 14 entries (*perche*, *perché* and *perchè*).

Still open, for later:

3. **Cloud model id.** The default `Anthropic:Model` is the one written in `CLAUDE.md` (`claude-sonnet-4-20250514`). It is never
   sent while the cloud is off, but check it before you ever switch the cloud on.
4. **What cannot be proved without a key.** The line that really calls the cloud service runs for the first time only when you
   switch the cloud on with a key. Everything around it is tested; that single call is not (research R2).

## Project Structure

### Documentation (this feature)

```text
specs/005-model-router/
├── plan.md              # This file
├── research.md          # Phase 0: decisions and alternatives
├── data-model.md        # Phase 1: types, options, decision table
├── quickstart.md        # Phase 1: how to validate
├── contracts/
│   ├── ask-routing-api.md   # preference in, backend and new failures out
│   └── routing.md           # ICloudAIProvider, ComplexityScorer, ModelRouter, configuration
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Enums/        ModelPreference.cs, ModelBackend.cs, RoutingFailureKind.cs, ComplexitySignals.cs (new);
│   │                 AskFailureKind.cs (add PreferenceNotAllowed)
│   ├── Interfaces/   ICloudAIProvider.cs (new)
│   ├── Ai/           RoutingOptions.cs, RoutingRequest.cs (new)
│   ├── Results/      RoutingResult.cs (new); AskResult.cs (backend)
│   ├── Services/     ComplexityScorer.cs, ModelRouter.cs (new); DocumentQuestionService.cs (route, then answer)
│   └── Dtos/         AskDtos.cs (preference, backend)
├── Scriptorium.Infrastructure/
│   ├── Ai/           AnthropicOptions.cs, ClaudeProvider.cs (new)
│   └── Scriptorium.Infrastructure.csproj   (the one approved package)
├── Scriptorium.API/
│   ├── Endpoints/    QuestionEndpoints.cs (parse preference, map PreferenceNotAllowed to 409)
│   ├── Program.cs    (register options, cloud provider, scorer, router)
│   └── appsettings.json   (Routing and Anthropic sections, no key)
└── Scriptorium.Web/src/app/documents/
    ├── ask/ask-panel.component.{ts,html,spec.ts}   (selector, label, reset to Auto)
    ├── document-api.service.{ts,spec.ts}           (send the preference)
    └── document.models.ts                          (preference type, backend in the response)

tests/
├── Scriptorium.Core.Tests/
│   ├── Services/     ComplexityScorerTests.cs, ModelRouterTests.cs (new); DocumentQuestionServiceTests.cs (extended)
│   └── Results/ Dtos/  RoutingResultTests.cs (new); AskResult and DTO tests (extended)
└── Scriptorium.Infrastructure.Tests/
    ├── Ai/           ClaudeProviderTests.cs (new); AiConfigurationTests.cs (routing configuration)
    └── Endpoints/    QuestionEndpointTests.cs (extended)

docs: README.md (roadmap, configuration table, ask endpoint), docs/architecture.md (section 7)
```

**Structure Decision**: the existing four-project layout and its test projects. The decision and the score are in Core because
they are business rules and need no I/O; the cloud library lives only in Infrastructure; the API maps; the web page shows. No new
project.

## Suggested order for the tasks

Each step leaves the build and the tests green, and the first three change nothing the user can see.

1. **Foundation (Core)**: the enums, `RoutingOptions`, `RoutingRequest`, `RoutingResult`, `ICloudAIProvider`; then
   `ComplexityScorer` and `ModelRouter` with their tests (the whole decision table and the examples of the spec).
2. **Use case (Core)**: `DocumentQuestionService`, `AskResult`, the DTOs, with the tests extended; the existing cases keep their
   assertions.
3. **Cloud provider (Infrastructure)**: add the approved package and check its dependencies, `AnthropicOptions`,
   `ClaudeProvider` with its tests (no network).
4. **API and wiring**: the endpoint, `Program.cs`, `appsettings.json`, the endpoint tests (cloud off by default; cloud on with a
   stand-in).
5. **Web**: the selector, the label, the reset, the specs.
6. **Docs and final run**: README, architecture document, quickstart scenarios, `validation-results.md`.

User stories map to this order: US1 (privacy and default local) is steps 1 to 4; US2 (the preference) adds the selector in step
5 and its server checks in step 4; US3 (switch the cloud on later) is proved by the cloud-on tests of steps 1, 3 and 4.

## Complexity Tracking

No violation of the constitution to justify. The one package is approved; the SC-003 reading was settled with the user (see
"Points for the user").
