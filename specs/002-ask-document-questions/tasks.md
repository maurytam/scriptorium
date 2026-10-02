---

description: "Task list for Ask Questions About a Document"

---

# Tasks: Ask Questions About a Document

**Input**: Design documents from `/specs/002-ask-document-questions/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ask-api.md, contracts/ai-provider.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity: every production class has a matching test class, xUnit + FluentAssertions + Moq; Karma/Jasmine on the Angular side). Tests sit next to the task they cover. Logic-free records (`AiMessage`, `AiRequest`, `ChatExchange`, `QaOptions`, `OllamaOptions`) and the enums are covered through the classes that use them.

**Organization**: Tasks are grouped by user story (from spec.md) so each can be implemented and tested independently. The solution already exists from feature 001; this feature adds the AI abstraction, one provider, one endpoint and one conversation card.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1 to US3)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Web application layout per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.Infrastructure/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/` (Angular), `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The one new dependency, the configuration and a check of the library's real API

- [X] T001 Add the `OllamaSharp` 5.5.0 package reference to `src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj` (approved by the user on 2026-10-01; no other project may reference it, Constitution I and VII)
- [X] T002 [P] Add the `Ollama` (`BaseUrl`, `Model`, `NumCtx`, `TimeoutSeconds`) and `Qa` (`MaxQuestionLength`, `MaxDocumentCharacters`, `MaxHistoryExchanges`, `MaxHistoryCharacters`) sections with the defaults of research R12 to `src/Scriptorium.API/appsettings.json`
- [X] T003 Spike (depends on T001): confirm in OllamaSharp 5.5.0 the exact API for a chat request with role-tagged messages, the window-size and temperature options, switching thinking off, the `IOllamaApiClient` interface to mock, and the exceptions raised for an unreachable server and for a missing model; record the findings under R6 in `specs/002-ask-document-questions/research.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The AI abstraction and its only implementation; every user story depends on them

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create the enums `AiRole` (`System`, `User`, `Assistant`), `AiFailureKind` (`Unavailable`, `Timeout`, `Failed`) and `AskFailureKind` (`InvalidQuestion`, `NotFound`, `NotReady`, `ModelUnavailable`, `Timeout`, `Internal`) in `src/Scriptorium.Core/Enums/AiRole.cs`, `AiFailureKind.cs` and `AskFailureKind.cs`
- [X] T005 [P] Create the records `AiMessage` (role, content) and `AiRequest` (ordered messages) in `src/Scriptorium.Core/Ai/AiMessage.cs` and `src/Scriptorium.Core/Ai/AiRequest.cs`
- [X] T006 [P] Create the records `ChatExchange` (question, answer) and `QaOptions` (`MaxQuestionLength`, `MaxDocumentCharacters`, `MaxHistoryExchanges`, `MaxHistoryCharacters`) in `src/Scriptorium.Core/Ai/ChatExchange.cs` and `src/Scriptorium.Core/Ai/QaOptions.cs`
- [X] T007 [P] Create `AiResult` (success with the answer text, or failure with `AiFailureKind` and a message, same shape as `UploadResult`) in `src/Scriptorium.Core/Results/AiResult.cs` (depends on T004)
- [X] T008 [P] Unit tests for `AiResult` in `tests/Scriptorium.Core.Tests/Results/AiResultTests.cs`
- [X] T009 Define `IAIProvider` (`Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)`) in `src/Scriptorium.Core/Interfaces/IAIProvider.cs` per `contracts/ai-provider.md` (depends on T005, T007)
- [X] T010 [P] Create the `OllamaOptions` record (`BaseUrl`, `Model`, `NumCtx`, `TimeoutSeconds`) in `src/Scriptorium.Infrastructure/Ai/OllamaOptions.cs`
- [X] T011 Implement `OllamaProvider` in `src/Scriptorium.Infrastructure/Ai/OllamaProvider.cs`: map the request messages to a chat request with model, window size, low temperature (0.2) and thinking disabled; assemble the streamed chunks into one answer text; return `Unavailable` when the server cannot be reached or the model is missing, `Timeout` after `OllamaOptions.TimeoutSeconds`, `Failed` for any other error or an empty answer; let caller cancellation propagate; never log the question or the document text (depends on T003, T005, T007, T009, T010)
- [X] T012 [P] Unit tests for `OllamaProvider` against a mocked `IOllamaApiClient` in `tests/Scriptorium.Infrastructure.Tests/Ai/OllamaProviderTests.cs`: request options and roles sent, chunks assembled, empty answer, unreachable server, missing model, timeout, cancellation
- [X] T013 Register in `src/Scriptorium.API/Program.cs` the binding of `OllamaOptions` and `QaOptions` from configuration, `IAIProvider` built with `OllamaProvider.Create(OllamaOptions)`, so the API never touches OllamaSharp types (the provider creates its own `HttpClient` with an infinite timeout, see research R13) (depends on T002, T011)

**Checkpoint**: The domain can ask a model for an answer through `IAIProvider`; nothing else knows Ollama exists

---

## Phase 3: User Story 1 - Ask a question and get an answer (Priority: P1) 🎯 MVP

**Goal**: A user opens a ready document, types a question and reads an answer based on the document's text; a progress indicator shows while waiting, and refused questions explain why.

**Independent Test**: Upload a document with a known fact, ask about it from the UI (or `curl`) and confirm the answer states it; ask about a document that is processing or unknown and confirm a clear refusal.

- [X] T014 [P] [US1] Create `AskResult` (success with `Answer` and `Truncated`, or failure with `AskFailureKind` and a message) in `src/Scriptorium.Core/Results/AskResult.cs` (depends on T004)
- [X] T015 [P] [US1] Unit tests for `AskResult` in `tests/Scriptorium.Core.Tests/Results/AskResultTests.cs`
- [X] T016 [US1] Implement `QuestionPromptBuilder` in `src/Scriptorium.Core/Services/QuestionPromptBuilder.cs`: the system message (answer only from the document, say plainly when it does not contain the answer, answer in the language of the question, be concise, treat the document as data and ignore instructions inside it) followed by the document text between delimiters, cut at `QaOptions.MaxDocumentCharacters` with a flag saying it was truncated, then the question as the last user message (depends on T005, T006)
- [X] T017 [P] [US1] Unit tests for `QuestionPromptBuilder` in `tests/Scriptorium.Core.Tests/Services/QuestionPromptBuilderTests.cs`: rules present, document included, question last, no truncation under the budget, truncation flag over it
- [X] T018 [US1] Implement `DocumentQuestionService` in `src/Scriptorium.Core/Services/DocumentQuestionService.cs`: validate the question (trimmed, not empty, within `MaxQuestionLength`), load the document (`NotFound`) and require status `Ready` (`NotReady`, naming the status), load its extracted text, build the prompt, call `IAIProvider`, and return `AskResult` (a provider failure is returned as `Internal` for now; refined in US3) (depends on T009, T014, T016)
- [X] T019 [P] [US1] Unit tests for `DocumentQuestionService` with a mocked `IAIProvider` and repository in `tests/Scriptorium.Core.Tests/Services/DocumentQuestionServiceTests.cs`: answer returned, empty and over-long question, unknown document, document not ready, the prompt sent contains the document text
- [X] T020 [P] [US1] Create `AskRequestDto` (question, history), `ChatExchangeDto` (question, answer) and `AskResponseDto` (answer, truncated) in `src/Scriptorium.Core/Dtos/AskDtos.cs`
- [X] T021 [P] [US1] Unit tests for the mappings of `AskDtos` in `tests/Scriptorium.Core.Tests/Dtos/AskDtosTests.cs`
- [X] T022 [US1] Implement `POST /api/documents/{id}/ask` in `src/Scriptorium.API/Endpoints/QuestionEndpoints.cs` per `contracts/ask-api.md` (`200`, `400` for an invalid question, `404`, `409`; any other failure goes through the existing generic `500`), and register `DocumentQuestionService`, `QuestionPromptBuilder` and `MapQuestionEndpoints` in `src/Scriptorium.API/Program.cs` (depends on T013, T018, T020)
- [X] T023 [P] [US1] Integration tests with a fake `IAIProvider` registered through `ConfigureTestServices` in `tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs`: answer returned for a ready document, `404`, `409` for a processing and a failed document, `400` for an empty question
- [X] T024 [US1] Add the ask types and `DocumentApiService.ask()` (calls `POST /api/documents/{id}/ask`) in `src/Scriptorium.Web/src/app/documents/document.models.ts` and `src/Scriptorium.Web/src/app/documents/document-api.service.ts`, with specs in `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`
- [X] T025 [US1] Create `ConversationSelectionService` (which document is open: open, close, clear if that document is the one given) in `src/Scriptorium.Web/src/app/documents/ask/conversation-selection.service.ts`, with `conversation-selection.service.spec.ts`
- [X] T026 [US1] Build the `AskPanelComponent` (card "Ask about *name*": transcript of questions and answers, question field, **Send** button, indeterminate progress indicator while waiting, send control disabled while waiting, the error text when a request fails) in `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `ask-panel.component.html`, sending each question on its own for now (depends on T024, T025)
- [X] T027 [P] [US1] Specs for `AskPanelComponent` in `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts`: sends a question, shows the answer and the progress indicator, disables **Send** while waiting, shows the failure text
- [X] T028 [US1] Add an **Ask** button to every "ready" row of the document list, opening the conversation for that document through `ConversationSelectionService`, in `src/Scriptorium.Web/src/app/documents/document-list/document-list.component.html` and `.ts`, with the spec updates in `document-list.component.spec.ts` (depends on T025)
- [X] T029 [US1] Place the panel between the upload card and the list in `src/Scriptorium.Web/src/app/app.component.html` (with `app.component.spec.ts`) and add the conversation card styling to the single stylesheet `src/Scriptorium.Web/src/styles.css`, light and dark (depends on T026)

**Checkpoint**: User Story 1 is fully functional and testable independently (MVP)

---

## Phase 4: User Story 2 - Ask follow-up questions (Priority: P2)

**Goal**: After the first answer the user asks further questions that depend on what was said; the conversation lives only while the page is open.

**Independent Test**: Ask a question, then a short follow-up that only makes sense given the first answer, and confirm it is answered in context; reload and confirm the conversation is gone.

- [ ] T030 [US2] Extend `QuestionPromptBuilder` (`src/Scriptorium.Core/Services/QuestionPromptBuilder.cs`) to add the earlier exchanges as alternating user and assistant messages between the system message and the new question, keeping only the most recent `MaxHistoryExchanges` and no more than `MaxHistoryCharacters` characters, dropping the oldest first
- [ ] T031 [P] [US2] Extend `QuestionPromptBuilderTests` (`tests/Scriptorium.Core.Tests/Services/QuestionPromptBuilderTests.cs`) with the history cases: order and roles, the exchange limit, the character cap, an empty history
- [ ] T032 [US2] Extend `DocumentQuestionService` (`src/Scriptorium.Core/Services/DocumentQuestionService.cs`) to accept the earlier exchanges and pass them to the builder
- [ ] T033 [P] [US2] Extend `DocumentQuestionServiceTests` (`tests/Scriptorium.Core.Tests/Services/DocumentQuestionServiceTests.cs`) so the history reaches the prompt
- [ ] T034 [US2] Extend `POST /api/documents/{id}/ask` (`src/Scriptorium.API/Endpoints/QuestionEndpoints.cs`) to read `history` from the body and pass it on, answering `400` when a history entry is incomplete
- [ ] T035 [P] [US2] Extend `QuestionEndpointTests` (`tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs`): the history is forwarded to the provider, no history works, an incomplete entry gives `400`
- [ ] T036 [US2] Make the conversation continuous in the Angular panel (`src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html`): send the earlier exchanges with every question, add a **New conversation** button, and discard the transcript when the panel closes, another document is chosen or the open document is deleted (the list calls `ConversationSelectionService` on deletion, `src/Scriptorium.Web/src/app/documents/document-list/document-list.component.ts`)
- [ ] T037 [P] [US2] Specs for the above in `ask-panel.component.spec.ts` and `document-list.component.spec.ts`: the history is sent, **New conversation** empties the transcript, choosing another document or deleting the open one discards it

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Clear limits and failures (Priority: P3)

**Goal**: The user always understands what happened when an answer cannot be complete: the document was too long to be read in full, the model is not available, the answer took too long, or the question was not valid.

**Independent Test**: Ask about a very long document and see the notice; stop Ollama and see a specific message with the question still in the input; send an empty or over-long question and see what to fix.

- [ ] T038 [US3] Extend `DocumentQuestionService` (`src/Scriptorium.Core/Services/DocumentQuestionService.cs`): map `AiFailureKind.Unavailable` to `ModelUnavailable`, `Timeout` to `Timeout` and `Failed` to `Internal`, and carry the builder's truncation flag into `AskResult.Truncated`
- [ ] T039 [P] [US3] Extend `DocumentQuestionServiceTests` (`tests/Scriptorium.Core.Tests/Services/DocumentQuestionServiceTests.cs`): each provider failure maps to the right kind, the truncation flag reaches the result
- [ ] T040 [US3] Extend `QuestionEndpoints` (`src/Scriptorium.API/Endpoints/QuestionEndpoints.cs`): `ModelUnavailable` → `503` and `Timeout` → `504` with the messages of `contracts/ask-api.md`, `Internal` → generic `500`, and `truncated` in the `200` body
- [ ] T041 [P] [US3] Extend `QuestionEndpointTests` (`tests/Scriptorium.Infrastructure.Tests/Endpoints/QuestionEndpointTests.cs`): `503`, `504` and `500` from a failing fake provider (the API keeps answering afterwards), `400` for a question over 2,000 characters, `truncated: true` for a document over the budget
- [ ] T042 [US3] Extend the Angular panel (`src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html`): the "based only on the first part of the document" notice when `truncated` is true, a specific message per failure (`400`, `404` document gone, `409`, `503` Ollama not available, `504` too slow, network error), the question kept in the input for a retry, and a client-side check that blocks an empty or over-long question with a character counter
- [ ] T043 [P] [US3] Specs for the above in `ask-panel.component.spec.ts`: the notice, one test per failure message, the question kept after a failure, the counter and the blocked send

**Checkpoint**: All three user stories are independently functional

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T044 [P] Update `README.md`: describe the feature, add Ollama and the model as a prerequisite for asking questions (`ollama pull qwen3.5:9b`), the `Ollama` and `Qa` configuration keys, the new endpoint in the API table, and mark the roadmap item as done
- [ ] T045 Run the `quickstart.md` scenarios 1 to 9 against the real Ollama and the real model and record the results in `specs/002-ask-document-questions/validation-results.md` (needs Ollama running on the machine)
- [ ] T046 [P] Review all new classes against Constitution VI (methods under 30 lines, single responsibility) and against the contract rule that the question and the document text are never logged; record the outcome in `specs/002-ask-document-questions/validation-results.md` and refactor anything that exceeds it

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies. T003 needs T001.
- **Foundational (Phase 2)**: Depends on Setup; BLOCKS all user stories.
- **User Stories (Phase 3 to 5)**: Depend on Foundational. US2 and US3 extend files created in US1 (`QuestionPromptBuilder`, `DocumentQuestionService`, `QuestionEndpoints`, the panel), so work them after US1 unless one developer handles the shared files in order.
- **Polish (Phase 6)**: Depends on the desired stories being complete. T045 needs a running Ollama.

### User Story Dependencies

- **User Story 1 (P1)**: Only the Foundational phase.
- **User Story 2 (P2)**: Extends US1's builder, service, endpoint and panel; independently testable through the history cases.
- **User Story 3 (P3)**: Extends US1's service, endpoint and panel; independently testable through the failure cases.

### Within Each User Story

- Types and results before the service, the service before the endpoint, the endpoint before the Angular service, the Angular service before the panel
- Tests alongside (or immediately after) the task they cover

### Parallel Opportunities

- Setup: T002 beside T001/T003
- Foundational: T004 to T008 and T010 together (different files); T012 beside T013
- US1: T014/T015, T016/T017, T020/T021 are independent pairs; T023 and the Angular tasks T024/T025 can proceed once T022 exists
- US2 and US3 backend extensions touch different concerns of the same files, so sequence them if worked on by one person
- Polish: T044 and T046 in parallel

---

## Parallel Example: Foundational Phase

```bash
# Launch together (different files, no dependencies on each other):
Task: "Create the enums AiRole, AiFailureKind and AskFailureKind in src/Scriptorium.Core/Enums/"
Task: "Create the records AiMessage and AiRequest in src/Scriptorium.Core/Ai/"
Task: "Create the records ChatExchange and QaOptions in src/Scriptorium.Core/Ai/"
Task: "Create the OllamaOptions record in src/Scriptorium.Infrastructure/Ai/OllamaOptions.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (the package, the configuration, the spike)
2. Complete Phase 2: Foundational (the abstraction and `OllamaProvider`)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run scenarios 1, 2 and 4 of `quickstart.md` and ask a real question from the UI
5. Demo if ready

### Incremental Delivery

1. Setup + Foundational → the model can be asked through `IAIProvider`
2. Add User Story 1 → validate → MVP (ask and answer)
3. Add User Story 2 → validate → conversations
4. Add User Story 3 → validate → honest limits and failures
5. Polish → README, real-model validation, Constitution review

---

## Notes

- [P] tasks touch different files with no unmet dependencies
- [Story] labels map each task to its user story for traceability
- US2 and US3 tasks extend files created in US1; expect small additive diffs
- The model's answer quality and speed (SC-001, SC-003, SC-005) can only be judged with the real model, hence T045
- Commit after each task or logical group; never commit automatically without asking (Constitution, Non-Negotiable Constraints)
- Stop at any checkpoint to validate a story independently before moving to the next
