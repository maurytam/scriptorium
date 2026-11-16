---

description: "Task list for the Model Router (cloud switched off)"

---

# Tasks: Model Router (cloud switched off)

**Input**: Design documents from `/specs/005-model-router/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ask-routing-api.md, contracts/routing.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity). Backend: xUnit + FluentAssertions + Moq, plus endpoint tests on the in-process `ApiTestHost`. Frontend: Jasmine/Karma specs next to each component and service. Tests are written before the code they cover and must fail first (a test that does not compile yet counts as failing). All test documents are fake files; no document uploaded by the user is read or used. **No test and no manual step calls the real cloud service or needs a key**: the cloud-on cases use a stand-in `ICloudAIProvider`.

**Organization**: Tasks are grouped by user story (from spec.md). The routing types, the scorer, the router and the cloud provider are shared by all three stories, so they sit in Foundational. User Story 1 wires the router into the question flow (every question is routed, a private document stays local, the answer says which backend answered). User Story 2 adds the preference (endpoint and selector) and the refusals. User Story 3 proves with stand-ins that switching the cloud on is only configuration. Only one NuGet package is added: `Anthropic` 13.8.0, approved by the user on 2026-11-16 because it is free. No npm package, no migration.

**Commits**: Nothing is committed until the user has tried the story with the project's manual check and given the OK (project rule). Work stays on branch `007-model-router`. There is **one** pull request for the whole feature, opened only after the user's OK, never on `main` directly.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.Infrastructure/Ai/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/src/app/documents/`, `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/`. Manual-test files go in `~/Desktop/prove-scriptorium` (fake content only, different content each). Manual checks run on a throwaway API on port 5099 with its own database; the user's API on 5034 is never touched, and a test server is stopped only by the PID of its own port.

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" can be checked at the end

- [X] T001 Run `dotnet build` and `dotnet test` on branch `007-model-router` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`, before any change; note the build warnings and the number of passing tests per test project (backend: Core and Infrastructure; frontend: Karma). Expected: Core 117, Infrastructure 187, frontend 131, 0 warnings. If the frontend tests cannot run (no Chrome, missing `node_modules`), say so to the user before going on (reference for T037 and T046)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The routing types, the scorer, the router and the cloud provider, which every story needs. Nothing here changes what the user sees.

**⚠️ CRITICAL**: No story work can start until this is complete

### Tests for the Core (write first, they must fail) ⚠️

- [X] T002 [P] Create `tests/Scriptorium.Core.Tests/Services/ComplexityScorerTests.cs` for `ComplexityScorer.Score(question, documentLength, earlierExchanges)` with `QaOptions` default (`MaxDocumentCharacters` 8,000), as theories: **long question** (399 characters no, 400 yes); **several questions** (one `?` no, two yes); **long reading** (5,999 no, 6,000 yes, and a length of 50,000 counts as 8,000 read, so yes; with `MaxDocumentCharacters` 1,000 the limit is 750); **conversation under way** (2 earlier exchanges no, 3 yes); **reasoning words**: each of the 14 entries of the fixed list fires the signal (`compare`, `explain why`, `analyze`, `differences`, `implications`, `pros and cons`, `confronta`, `perche`, `perché`, `perchè`, `analizza`, `differenze`, `implicazioni`, `pro e contro`), in any letter case and with any run of spaces inside a phrase; a word inside another word does not (for example `incomparable`); no word, no signal; no signal gives 0 and all five give 5, with `Signals` naming exactly the ones that fired; several matching words count as **one** point; and the three examples of the plan: "Qual è il codice finale?" on a 20,000-character document scores 1, "Confronta le penali e spiega perché differiscono?" on the same document scores 2, and a question with a reasoning word and two `?` on a long document with 3 earlier exchanges scores 4. The scorer's signature takes no document text (compile-level proof of the privacy rule)
- [X] T003 [P] Create `tests/Scriptorium.Core.Tests/Services/ModelRouterTests.cs` with `Mock<IAIProvider>` (local) and `Mock<ICloudAIProvider>` (cloud), both `MockBehavior.Strict` so any call fails the test: the **whole decision table** of data-model.md as one theory (private yes/no × preference Auto/Local/Cloud × cloud state off / on and configured / on but not configured × score below, at and above the threshold 2) giving the expected backend or failure kind; the success carries the right provider instance (the local mock for Local, the cloud mock for Cloud) and the right `ModelBackend`; the refusal for a private document with Cloud is the same whether the cloud is on or off and takes precedence over "cloud not available"; the failure messages are exactly `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` and `This document is private, so it can only be answered on this computer. Choose Auto or Local.`; a score equal to the threshold stays Local under Auto; the threshold is read from `RoutingOptions` (with 0, a score of 1 is complex; with 5, nothing is); the router never calls a provider
- [X] T004 [P] Create `tests/Scriptorium.Core.Tests/Results/RoutingResultTests.cs` in the style of `AskResultTests`: a success carries `Backend` and `Provider`, `IsSuccess` is true and there is no failure kind or error; a failure carries its kind and error and `IsSuccess` is false

### Implementation of the Core

- [X] T005 [P] Create the enums in `src/Scriptorium.Core/Enums/`: `ModelPreference.cs` (`Auto`, `Local`, `Cloud`), `ModelBackend.cs` (`Local`, `Cloud`), `RoutingFailureKind.cs` (`CloudNotAvailable`, `PrivateDocumentCloudRefused`) and `ComplexitySignals.cs` (`[Flags]`: `None = 0`, `LongQuestion`, `SeveralQuestions`, `LongReading`, `ConversationUnderWay`, `ReasoningWords`, one bit each)
- [X] T006 [P] Add `PreferenceNotAllowed` at the end of `AskFailureKind` in `src/Scriptorium.Core/Enums/AskFailureKind.cs` (the answer to "Cloud for a private document", `409`); do not reorder the existing values
- [X] T007 [P] Create `src/Scriptorium.Core/Ai/RoutingOptions.cs` (`public sealed record`, section `Routing`: `bool CloudEnabled { get; init; } = false`, `int ComplexityThreshold { get; init; } = 2`, in the style of `QaOptions`) and `src/Scriptorium.Core/Ai/RoutingRequest.cs` (`record RoutingRequest(bool IsPrivate, ModelPreference Preference, int ComplexityScore)`)
- [X] T008 Create the interface `ICloudAIProvider : IAIProvider` in `src/Scriptorium.Core/Interfaces/ICloudAIProvider.cs` with `bool IsConfigured { get; }` and the doc comments of contracts/routing.md. **Written before `ClaudeProvider` (T017)**
- [X] T009 Create `src/Scriptorium.Core/Results/RoutingResult.cs`, shaped like `AskResult` (private constructor, `Success(ModelBackend backend, IAIProvider provider)`, `Failure(RoutingFailureKind kind, string error)`, `IsSuccess` with `MemberNotNullWhen` for `Provider` and for `FailureKind`/`Error`, no `!`)
- [X] T010 Create `ComplexityScorer` in `src/Scriptorium.Core/Services/ComplexityScorer.cs` (a `public sealed class` receiving `QaOptions`) and the small record `ComplexityScore(ComplexitySignals Signals)` with `int Value` (the number of bits set): `Score(string question, int documentLength, int earlierExchanges)` applies the five signals of research R3 with named constants (400 characters, 2 question marks, 75 % of `MaxDocumentCharacters` computed on the length clamped to that maximum, 3 earlier exchanges, the fixed list of 14 words and phrases matched case-insensitively, as whole words, with any run of whitespace inside a phrase). Pure, no I/O, never receives the document text. One small method per signal, each under 30 lines
- [X] T011 Create `ModelRouter` in `src/Scriptorium.Core/Services/ModelRouter.cs` (a `public sealed class` receiving the local `IAIProvider`, the `ICloudAIProvider` and `RoutingOptions`) with `RoutingResult Route(RoutingRequest request)` applying the rules of research R1 in order; the cloud is usable when `CloudEnabled` and `cloud.IsConfigured`; "complex" is `ComplexityScore > ComplexityThreshold`; the two failure messages are constants with the exact text of T003. It only hands a provider back, never calls it. One small method per rule, each under 30 lines
- [X] T012 Run `dotnet build` and `dotnet test` on `tests/Scriptorium.Core.Tests`: T002, T003 and T004 pass and every existing Core test is still green

### Cloud provider (Infrastructure)

- [X] T013 Add the one approved package: `dotnet add src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj package Anthropic --version 13.8.0`. Then run `dotnet list src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj package --include-transitive` and compare what `Anthropic` brings with the table of research R4 (`Microsoft.Extensions.AI.Abstractions` 10.5.1, `System.Net.ServerSentEvents` 10.0.1, `System.Text.Json` 10.0.6). Report any difference to the user **before going on**. Confirm with `git diff --stat` that only `Scriptorium.Infrastructure.csproj` (and a lock file, if the repository has one) changed: no other `.csproj`, no `package.json`, no second package
- [X] T014 Look at the public surface of the `Anthropic` 13.8.0 library (its XML docs or its types in the restored package, without calling anything): how a client is created, whether it accepts a custom `HttpClient`, which exceptions it throws for a missing key, an unreachable service and a timeout. Decide the seam of research R2 (a fake `HttpMessageHandler`, or a small internal interface wrapping the library as `OllamaProvider` does with `IOllamaApiClient`) and write the outcome in `specs/005-model-router/research.md` under R2. If the surface differs from what R2 assumes, stop and tell the user before going on
- [X] T015 [P] Create `src/Scriptorium.Infrastructure/Ai/AnthropicOptions.cs` (`public sealed record`, section `Anthropic`: `string? ApiKey` with no default, `string Model = "claude-sonnet-4-20250514"`, `int TimeoutSeconds = 120`, `int MaxTokens = 1024`, in the style of `OllamaOptions`), with a comment that the key comes only from user-secrets or the environment
- [X] T016 Create `tests/Scriptorium.Infrastructure.Tests/Ai/ClaudeProviderTests.cs` in the style of `OllamaProviderTests`, using the seam chosen in T014, **with no network**: with no key (null, empty, whitespace) `IsConfigured` is false and `CompleteAsync` returns an `Unavailable` failure **without touching the seam or the handler** (it fails the test if called); building the provider with no key does **not** create the library's client and does not throw (so the API can start with the delivered settings), and a key present in an environment variable of the library itself is ignored: only `AnthropicOptions.ApiKey` counts; with a key `IsConfigured` is true; a reply gives `AiResult.Success` with the text trimmed; an empty reply gives `Failed`; a timeout gives `Timeout` with `The cloud model did not answer in time. Please try again.`; an unreachable service gives `Unavailable` with `The cloud model is not available. Check the cloud settings and your connection.`; any other library failure gives `Failed` with `The cloud model could not produce an answer.`; cancellation by the caller propagates as `OperationCanceledException`; the messages say "cloud model", never "Ollama"
- [X] T017 Create `ClaudeProvider : ICloudAIProvider` in `src/Scriptorium.Infrastructure/Ai/ClaudeProvider.cs`, the only class that knows the cloud library, shaped like `OllamaProvider` (a constructor taking the seam and `AnthropicOptions`, a static `Create(AnthropicOptions)` that builds the real client, its own timeout from `TimeoutSeconds`, expected problems returned as `AiResult` failures). The library's client is created **lazily and only when a key is present**: building the provider (including `Create`) never creates it, never throws and never reads a key from the environment on its own, so the API starts with the delivered settings and no key. With no key `CompleteAsync` returns `Unavailable` **before building any request**. No Ollama code, no `ModelRouter` logic. Methods under 30 lines
- [X] T018 Run `dotnet test`: T016 passes and everything else is still green. **Checkpoint**: the foundation is in place; nothing the user sees has changed

---

## Phase 3: User Story 1 - Every question is routed, and a private document never leaves the machine (Priority: P1) 🎯 MVP

**Goal**: Every question passes through the router; with the delivered settings (cloud off) all of them go to Ollama exactly as before, a private document is always local, and the answer says it was answered locally.

**Independent Test**: With the delivered settings, ask a private and a non-private document questions and see they are answered by Ollama with the label "Answered on this computer"; the existing question tests still pass with the same assertions.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [X] T019 [US1] Update `tests/Scriptorium.Core.Tests/Services/DocumentQuestionServiceTests.cs`. **Mechanical part (no assertion changes)**: build the service with a real `ModelRouter` over the strict `Mock<IAIProvider>` (local), a strict `Mock<ICloudAIProvider>` that must never be called, `RoutingOptions` default and a real `ComplexityScorer`; give every `AskAsync` call the argument `ModelPreference.Auto` before the cancellation token. **New cases**: a private document under every preference but Cloud is answered by the local provider and the cloud mock is never called; the answer carries `Backend` `Local`; a routing failure (preference Cloud with the cloud off) returns `AskResult.Failure(ModelUnavailable, ...)` with the router's message and **no provider is called**; Cloud for a private document returns `AskResult.Failure(PreferenceNotAllowed, ...)` with the router's message and no provider is called; the routing happens **after** the not-found, not-ready and invalid-question checks (those messages win); the score is computed from the **trimmed** question, the text length and the number of earlier exchanges (a conversation of three exchanges adds a point, checked through a stand-in cloud that is on and a threshold of 0 in a router built for the test)
- [X] T020 [P] [US1] Extend `tests/Scriptorium.Core.Tests/Results/AskResultTests.cs` and `tests/Scriptorium.Core.Tests/Dtos/AskDtosTests.cs` **without changing the existing assertions**: `AskResult.Success("Paris", truncated)` (two arguments) still compiles and has `Backend` `Local`; `Success("Paris", false, ModelBackend.Cloud)` carries `Cloud`; `AskResponseDto.From` copies the backend as `"local"` or `"cloud"`; `new AskResponseDto("Paris", truncated)` equals the DTO of a local answer
- [X] T021 [P] [US1] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs` and `tests/Scriptorium.Infrastructure.Tests/Ai/AiConfigurationTests.cs` (the existing tests stay as they are): with the delivered settings a successful answer has `"backend":"local"` and the mocked local provider was asked; a private document is answered locally with the same result; `IAIProvider` still resolves to `OllamaProvider`, `ICloudAIProvider` resolves to `ClaudeProvider` with `IsConfigured` false, `RoutingOptions` has `CloudEnabled` false and `ComplexityThreshold` 2 from `appsettings.json`, and the `Anthropic` section has no `ApiKey`

### Implementation for User Story 1

- [X] T022 [US1] Add the backend to the results: in `src/Scriptorium.Core/Results/AskResult.cs` give `Success` a third parameter `ModelBackend backend = ModelBackend.Local` and a `Backend` property (failures keep `Local`, unused); in `src/Scriptorium.Core/Dtos/AskDtos.cs` give `AskResponseDto` a third positional member `string Backend = "local"` and make `From` fill it with `"local"` or `"cloud"` from the result. The defaults keep every existing call and test compiling unchanged
- [X] T023 [US1] Change `DocumentQuestionService` in `src/Scriptorium.Core/Services/DocumentQuestionService.cs` as in contracts/routing.md: the constructor receives `IDocumentRepository`, `ModelRouter`, `ComplexityScorer`, `QuestionPromptBuilder` and `QaOptions` (no provider); `AskAsync(Guid documentId, string question, IReadOnlyList<ChatExchange>? history, ModelPreference preference, CancellationToken ct)`; the private `DocumentText` also carries the document's `IsPrivate`; after validation, loading and **before** building the prompt it scores, routes, returns the failure on a refusal (`CloudNotAvailable` → `ModelUnavailable`, `PrivateDocumentCloudRefused` → `PreferenceNotAllowed`, with the router's message) and otherwise builds the prompt and calls **the provider the router returned**, answering with the backend. Keep every method under 30 lines (extract small steps). The provider failure mapping (`Unavailable`, `Timeout`, other) stays as it is
- [X] T024 [US1] Make `src/Scriptorium.API/Endpoints/QuestionEndpoints.cs` compile with the new `AskAsync`: pass `ModelPreference.Auto` for now (User Story 2 reads the request's preference)
- [X] T025 [US1] Wire it in `src/Scriptorium.API/Program.cs` and `src/Scriptorium.API/appsettings.json`: read the `Routing` section into `RoutingOptions` and the `Anthropic` section into `AnthropicOptions` (like `Ollama` and `Qa`), register them as singletons; register `ICloudAIProvider` as `ClaudeProvider.Create(anthropicOptions)`; register `ComplexityScorer` and `ModelRouter`; leave `IAIProvider` registered as `OllamaProvider`. In `appsettings.json` add `"Routing": { "CloudEnabled": false, "ComplexityThreshold": 2 }` and `"Anthropic": { "Model": "claude-sonnet-4-20250514", "TimeoutSeconds": 120, "MaxTokens": 1024 }` with **no `ApiKey`**
- [X] T026 [US1] Run `dotnet build` and `dotnet test`: T019, T020 and T021 pass, the existing question tests pass with the same assertions, no new warning

### Manual check for User Story 1

- [X] T027 [US1] With the user: throwaway API on 5099 (quickstart prerequisites) and fake files in `~/Desktop/prove-scriptorium` (a non-private `.txt` and a different private `.txt`); run quickstart scenarios 1 and 2 (Ollama needed). The label "Answered on this computer" arrives in the panel only with User Story 2 (T036): here the page must behave exactly as before, and the `backend` field is checked with `curl` on the throwaway API. **Wait for the user's OK before anything is committed**; record the outcome in `specs/005-model-router/validation-results.md`

**Checkpoint**: Every question is routed, the Private flag now has an effect, and the answer says where it came from

---

## Phase 4: User Story 2 - The person can choose Auto, Local or Cloud (Priority: P2)

**Goal**: The person can pick a preference before asking. Auto and Local are answered locally; Cloud while the cloud is off gives the explicit error, and Cloud for a private document is refused, each with a clear message.

**Independent Test**: In the question panel pick each preference and ask: Auto and Local answer locally, Cloud shows the "not available" message, Cloud on the private document shows the "private" message, and the selector is back on Auto when the conversation is reset.

### Tests for User Story 2 (write first, they must fail) ⚠️

- [X] T028 [P] [US2] Extend `tests/Scriptorium.Core.Tests/Dtos/AskDtosTests.cs`: `AskRequestDto` has an optional `Preference` that is null when absent, and the existing construction `new AskRequestDto("q")` and `new AskRequestDto("q", history)` still compile
- [X] T029 [P] [US2] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs` with the cases of contracts/ask-routing-api.md: `preference` absent, `null`, empty or `"auto"` behaves as Auto; `"local"` and `"LOCAL"` are answered by the mocked local provider; `"cloud"` with the delivered settings gives `503` with `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` and the local provider is **never** called; `"cloud"` on a private document gives `409` with `This document is private, so it can only be answered on this computer. Choose Auto or Local.` and no provider is called; `"turbo"` gives `400` with `The preference must be auto, local or cloud.` and no provider is called; an unknown document, a document not ready and an invalid question keep their `404`, `409` and `400` (those win over routing)
- [X] T030 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`: `ask` sends the preference in the body (`{ question, history, preference }`), defaults to `'auto'` when none is given, and the response type carries `backend`
- [X] T031 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts`: the selector offers Auto, Local and Cloud with Auto selected; the chosen preference is sent with the question; each answer shows the label `Answered on this computer` (FR-018) for `backend: 'local'` and `Answered by the cloud model` for `'cloud'`; the selector goes back to Auto when another document is opened, when the panel is closed and on **New conversation**; the Cloud option is selectable (never disabled); a `503` and a `409` response show the server's message as it is; the introductory hint no longer claims that every answer is produced on the computer. Existing specs stay as they are

### Implementation for User Story 2

- [X] T032 [US2] Add the optional `string? Preference = null` as the last member of `AskRequestDto` in `src/Scriptorium.Core/Dtos/AskDtos.cs`
- [X] T033 [US2] In `src/Scriptorium.API/Endpoints/QuestionEndpoints.cs` parse `body?.Preference` (absent, null or empty is Auto; `auto`, `local`, `cloud` in any letter case; anything else is `400` with `The preference must be auto, local or cloud.` before any other work) and pass the result to `AskAsync` instead of the fixed `ModelPreference.Auto`; map `AskFailureKind.PreferenceNotAllowed` to `409` in `ToFailureResponse`. Keep each method under 30 lines (a small `TryParsePreference` helper)
- [X] T034 [US2] Run `dotnet build` and `dotnet test`: T028 and T029 pass and everything else is still green
- [X] T035 [P] [US2] Update `src/Scriptorium.Web/src/app/documents/document.models.ts` (a type `AskPreference = 'auto' | 'local' | 'cloud'`, `backend: 'local' | 'cloud'` in `AskResponse`) and `src/Scriptorium.Web/src/app/documents/document-api.service.ts` (`ask(id, question, history, preference = 'auto')` sending `preference`)
- [X] T036 [US2] Update `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html`: a `preference` signal (default `'auto'`) reset to Auto in `reset()`; an **Answer with** selector (a native `<select>` with Auto, Local and Cloud, labelled for screen readers) next to the Send button; each exchange stores the `backend` and shows the small label; the hint reworded to say answers are produced on this computer unless Cloud is chosen or picked by Auto. The component sends the preference and shows `backend`; it decides nothing. Add the few styles needed to `src/Scriptorium.Web/src/styles.css` in the existing style (light and dark)
- [X] T037 [US2] Run in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T030 and T031 pass, everything else is still green and no new warning (compare with T001)

### Manual check for User Story 2

- [X] T038 [US2] With the user: run quickstart scenarios 3, 4, 5 and 6 (the endpoint scenario uses `curl` on the throwaway API). **Wait for the user's OK before anything is committed**; record the outcome in `specs/005-model-router/validation-results.md`

**Checkpoint**: The preference works end to end; every refusal and error has a message the person understands

---

## Phase 5: User Story 3 - The cloud can be switched on later without redesign (Priority: P3)

**Goal**: Prove, with stand-ins and with no key and no network, that switching the cloud on is only configuration: Auto sends a complex non-private question to the cloud, a private one and Local never, and switching it off sends everything back to the local model.

**Independent Test**: The automated tests below, then quickstart scenario 7 by hand (cloud on, still no key).

### Tests for User Story 3 (write first; the behaviour should already exist, so a failure points to a real gap) ⚠️

- [ ] T039 [P] [US3] Extend `tests/Scriptorium.Core.Tests/Services/DocumentQuestionServiceTests.cs` with a router built for the test with `CloudEnabled` true over a stand-in `ICloudAIProvider` that is configured: a complex non-private question under Auto is answered by the stand-in and the result has `Backend` `Cloud`; a question at the threshold is answered locally; a private document and the preference Local never reach the stand-in; preference Cloud on a non-private document reaches it whatever the complexity; the same router rebuilt with `CloudEnabled` false sends all of them to the local provider; when the stand-in answers `Unavailable` or `Timeout`, the result is `ModelUnavailable` or `Timeout` with the stand-in's text and the local provider is **never called** (no fallback)
- [ ] T040 [P] [US3] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs` with an `ApiTestHost` started with `Routing:CloudEnabled` true and the stand-in `ICloudAIProvider` registered in place of `ClaudeProvider` (as the existing tests replace `IAIProvider`): a complex Auto question gives `200` with `"backend":"cloud"`; the same question on a private document gives `"local"`; with the cloud on but the **real** `ClaudeProvider` and no key, `"cloud"` gives `503` with the not-available message and Auto with a complex question is answered locally; a stand-in that answers `Unavailable` gives `503` with its own text and one that answers `Timeout` gives `504`, and in both the local provider is never called (no fallback); no test registers a key or reaches the network

### Implementation for User Story 3

- [ ] T041 [US3] Run `dotnet test`: T039 and T040 pass. If one fails, it shows a gap in the router, the service or the wiring: fix the production code (never weaken the test) and tell the user what was found. No new production file is expected in this phase

### Manual check for User Story 3

- [ ] T042 [US3] With the user: restart the throwaway API with `Routing__CloudEnabled=true` and **no key** and run quickstart scenario 7 (Cloud chosen gives the not-available message and nothing is sent; Auto with a complex question on a long document is answered locally). Never use a real key. **Wait for the user's OK before anything is committed**; record the outcome in `specs/005-model-router/validation-results.md`

**Checkpoint**: All three stories work; the cloud is built, off, and ready to be switched on by configuration

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, review against the constitution, the final regression run, and the pull request

- [ ] T043 [P] Update `README.md`: the roadmap row "Model router…" becomes done with a note that the cloud is off by default; a line in "What it does today" about the preference and the label; the configuration table gets `Routing:CloudEnabled`, `Routing:ComplexityThreshold`, `Anthropic:ApiKey` (only through `dotnet user-secrets` or the environment, never a file), `Anthropic:Model`, `Anthropic:TimeoutSeconds` and `Anthropic:MaxTokens`; the ask endpoint line of the API table mentions `preference`, `backend` and the new `409` and `503` cases; the feature's contracts are linked
- [ ] T044 [P] Update `docs/architecture.md`: section 7 "Planned: the model router" becomes a description of what exists (the router, the scorer with its five signals, the cloud provider that is off by default and how it is switched on); the interfaces table gets `ICloudAIProvider` and `ClaudeProvider`; the question-flow diagram (4.2) gets the routing step and the refusals; the data section keeps saying nothing new is stored
- [ ] T045 Review the new and changed code against the constitution: `ComplexityScorer`, `ModelRouter`, `ClaudeProvider` and the changed `DocumentQuestionService` each have one responsibility; every method is under 30 lines (`AskAsync` in particular); no `!` without a comment, no `.Result`/`.Wait()`, no `dynamic` or reflection; **`ModelRouter` is the only place that chooses between local and cloud** (`grep` for other decisions on `ModelBackend` or `CloudEnabled` outside the router and the wiring); no `HttpClient` outside `OllamaProvider` and `ClaudeProvider`; no Ollama and no cloud code in the same class; the scorer never receives the document text. Run `git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'`: only code that reads the key option, **no key value anywhere**, and `appsettings.json` has no `ApiKey`. Run `dotnet list package --include-transitive` again and confirm that `Anthropic` 13.8.0 and its dependencies of research R4 are the only additions; confirm with `git diff main --name-only` that no `package.json` or `package-lock.json` changed and that only `Scriptorium.Infrastructure.csproj` changed among the project files. List any deviation and ask the user before fixing anything beyond the feature
- [ ] T046 Run `dotnet build`, the full `dotnet test`, and in `src/Scriptorium.Web` the full `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`; compare with the T001 baseline (no test removed, only additions, no new warnings); run quickstart scenarios 8, 9 and 10; complete `specs/005-model-router/validation-results.md` with the results of scenarios 1 to 10, saying plainly that the single line that calls the real cloud service was never run
- [ ] T047 Hand over to the user: summarise the work and the validation. **Only with the user's OK**: commit what is not yet committed (messages in English, descriptive, with the task references and the required `Co-Authored-By` line), push `007-model-router`, and open **one** pull request to `main` for the whole feature; merge it only when the user says so, then update `main` and delete the branch locally and on the remote. Never commit on `main`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **Foundational (Phase 2)**: after Setup; blocks all three stories
- **User Story 1 (Phase 3)**: after Foundational
- **User Story 2 (Phase 4)**: after User Story 1, because it changes the same endpoint, DTO and question service files and replaces the fixed `Auto` of T024
- **User Story 3 (Phase 5)**: after User Story 2 (it uses the preference `Cloud` at the endpoint); it adds tests only
- **Polish (Phase 6)**: after all three stories

### Within Each Story

- Foundational Core: tests (T002 to T004) first; T005, T006 and T007 first among the code (T010 needs T005 and T007; T009 needs T005; T011 needs T005 to T010); T008 before T017 (the interface is written before its implementation)
- Foundational cloud provider: T013 (the package) before T014 (look at the library) before T016 and T017; T015 before T016; T016 before T017
- Story 1: T019 to T021 first; T022 before T023 (the service returns the backend); T023 before T024 and T025 (they use its new constructor and signature); T026 closes the code, then the manual check
- Story 2: T028 to T031 first; T032 before T033; T034 closes the backend; T035 before T036; T037 closes the frontend, then the manual check
- Story 3: T039 and T040 first, then T041
- The manual checks (T027, T038, T042) come before any commit of the story

### Parallel Opportunities

- T002, T003 and T004 are in different files and can be written together
- T005, T006 and T007 are in different files and can be written together
- T015 can be written while T013 and T014 are being done
- T020 and T021 are in different files and can be written together; T028 to T031 are four different files; T035 can go in parallel with T034
- T039 and T040 are in different files; T043 and T044 are in different files
- **Not** parallel: T019, T039 (same test file, in different phases), T023, T033 and T036 (same service, endpoint and component files as the tasks before them)

### Parallel Example: Core tests

```text
# Write the three Core test files together before any production code:
Task: "T002 ComplexityScorerTests in tests/Scriptorium.Core.Tests/Services/ComplexityScorerTests.cs"
Task: "T003 ModelRouterTests in tests/Scriptorium.Core.Tests/Services/ModelRouterTests.cs"
Task: "T004 RoutingResultTests in tests/Scriptorium.Core.Tests/Results/RoutingResultTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 baseline
2. Foundational: T002 to T018 (Core types, scorer, router, the approved package and the cloud provider)
3. T019 to T026: route every question and say where the answer came from
4. T027: try it with the user
5. **Stop and validate**: the user tries scenarios 1 and 2 and gives the OK; only then commit (on `007-model-router`). Do not open the pull request yet: the preference is not selectable until User Story 2

### Incremental Delivery

1. Foundation + User Story 1 → tried by the user → commit
2. User Story 2 (the preference, the refusals, the selector) → tried by the user → commit
3. User Story 3 (cloud-on proof with stand-ins) → tried by the user → commit
4. Polish: README, architecture document, review, final regression run → commit → one pull request for the whole feature

---

## Notes

- [P] tasks = different files, no dependencies
- The decision lives only in `ModelRouter`; the page sends the preference and shows `backend`
- `AskResult.Success`, `AskResponseDto` and `AskRequestDto` get their new members with default values, so every existing call and test compiles unchanged; only the construction of `DocumentQuestionService` and its `AskAsync` calls change mechanically in `DocumentQuestionServiceTests` (SC-003 as adjusted: no existing test case is removed or has its expectations changed)
- The cloud stays **off** in every manual step; no key is set, no key is asked for, and the single line that really calls the cloud service is never run
- Fake files only: no document uploaded by the user is read or used for tests or diagnosis
- If a task shows that the library or the framework behaves differently from research.md (T013, T014), stop and update research.md before going on, and tell the user
