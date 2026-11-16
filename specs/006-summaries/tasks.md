---

description: "Task list for Summaries"

---

# Tasks: Summaries

**Input**: Design documents from `/specs/006-summaries/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/summary-api.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity). Backend: xUnit + FluentAssertions + Moq, plus endpoint tests on the in-process `ApiTestHost`. Frontend: Jasmine/Karma specs next to each component and service. Tests are written before the code they cover and must fail first (a test that does not compile yet counts as failing). All test documents are fake files; no document uploaded by the user is read or used. **No test and no manual step calls the real cloud service or needs a key**: the cloud-on cases use a stand-in `ICloudAIProvider`.

**Organization**: Tasks are grouped by user story (from spec.md). The helpers extracted from the question flow, the new failure kind and the shared pieces of the API sit in Foundational: they change **no behaviour** and every existing test keeps its assertions and its set-up. User Story 1 builds a summary end to end with the local model (prompt, service, DTOs, endpoint, page). User Story 2 adds the preference and the refusals (the router rules of feature 005, proved also with a stand-in cloud). User Story 3 adds the notice that says only the first part of a long document was read. **No NuGet package, no npm package, no migration, nothing stored.**

**Commits**: Nothing is committed until the user has tried the story with the project's manual check and given the OK (project rule). Work stays on branch `008-summaries`. There is **one** pull request for the whole feature, opened only after the user's OK, never on `main` directly; it is merged only when the user says so, then the branch is deleted locally and on the remote.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/src/app/documents/`, `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/` (it also holds the endpoint tests and the tests of the API helpers). Manual-test files go in `~/Desktop/prove-scriptorium` (fake content only, different content each). Manual checks run on a throwaway API with its own database in the scratchpad, on port 5099, or on 5034 **only when the user has stopped their own API**; it is stopped only by the PID of its own port, never with `pkill`, and the user's real database and documents are never used.

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" can be checked at the end, and have the fake files

- [ ] T001 Run `dotnet build` and `dotnet test` on branch `008-summaries` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`, before any change; note the build warnings and the number of passing tests per test project (backend: Core and Infrastructure; frontend: Karma). Expected: Core 269, Infrastructure 233, frontend 143, 0 warnings. If the frontend tests cannot run (no Chrome, missing `node_modules`), say so to the user before going on (reference for T033, T042 and T053)
- [ ] T002 [P] Check that the fake files `facts.txt`, `long.txt` (longer than 8,000 characters) and `router-private.txt` exist in `~/Desktop/prove-scriptorium`, and create there two fake files: `tiny.txt` with the content `Hello there.` (a few words) and `broken.docx` with the plain text `not really a docx` inside (a `.docx` name with no real Word content, so it ends up *failed*, for the "not ready" case). Do not read or use any other file of the user

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Move out of the question flow what the summary flow needs too, so nothing is copied. **Nothing here changes what the user sees**, and `DocumentQuestionServiceTests`, `QuestionPromptBuilderTests` and `QuestionEndpointTests` are not edited at all.

**⚠️ CRITICAL**: No story work can start until this is complete

### Tests for the extracted helpers (write first, they must fail) ⚠️

- [ ] T003 [P] Create `tests/Scriptorium.Core.Tests/Services/ReadyDocumentTextTests.cs` for `ReadyDocumentText.LoadAsync(IDocumentRepository, Guid, CancellationToken)`, which returns a `ReadyDocumentText` that is itself the result (`IsSuccess`, `Value` the text, `IsPrivate`, `FailureKind` an `AskFailureKind`, `Error`; the shape of today's private `DocumentText`), with a strict `Mock<IDocumentRepository>`: an unknown id gives `NotFound` with `Document not found.`; a document `Processing` or `Failed` gives `NotReady` with `The document is not ready. Current status: processing.` (and `failed.`) and the text is **not** read; a ready document with no extracted text gives `Internal` with `The text of the document is not available.`; a ready document gives the text and its `IsPrivate` (true and false); the cancellation token reaches the repository
- [ ] T004 [P] Create `tests/Scriptorium.Core.Tests/Services/AskFailureMapperTests.cs` for `AskFailureMapper`: `FromRouting` turns `PrivateDocumentCloudRefused` into `PreferenceNotAllowed` and `CloudNotAvailable` into `ModelUnavailable`, keeping the router's text; `FromProvider` turns `Unavailable` into `ModelUnavailable` and `Timeout` into `Timeout` keeping the provider's text, and anything else (`Failed`) into `Internal` with the generic `The answer could not be produced.` and **no detail** of the provider's text
- [ ] T005 [P] Create `tests/Scriptorium.Core.Tests/Services/PromptDocumentBlockTests.cs` for `PromptDocumentBlock.Wrap(text)`: the result is `<document>`, a line break, the text, a line break, `</document>`; a closing tag inside the text is neutralised to `</ document>` in any letter case so the text cannot close the block; text without the tag is unchanged
- [ ] T006 [P] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/ModelPreferenceParserTests.cs` for `ModelPreferenceParser.TryParse(string? value, out ModelPreference)`: null and empty are Auto and valid; `auto`, `local`, `cloud` in any letter case give Auto, Local and Cloud; a value that is only spaces, `cloud ` with a trailing space, and `turbo` are not valid
- [ ] T007 [P] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/AskFailureResponsesTests.cs` for `AskFailureResponses.From(AskFailureKind?, string)`: `InvalidQuestion` is 400, `NotFound` 404, `NotReady`, `PreferenceNotAllowed` and `NotEnoughText` are 409, `ModelUnavailable` 503, `Timeout` 504, `Internal` and null 500; each body is `{ "error": <message> }`

### Implementation for the foundation

- [ ] T008 Add `NotEnoughText` **at the end** of `src/Scriptorium.Core/Enums/AskFailureKind.cs` (the existing values keep their numbers)
- [ ] T009 Create `src/Scriptorium.Core/Services/ReadyDocumentText.cs`: the loader moved out of `DocumentQuestionService` (today's `LoadTextAsync` and its private nested `DocumentText`), as a sealed class that is its own result, with the properties `IsSuccess` (with the same `MemberNotNullWhen` attributes), `Value`, `IsPrivate`, `FailureKind` and `Error`, and a static `LoadAsync(IDocumentRepository repository, Guid documentId, CancellationToken ct)`; same messages as today. Doc comment, every method under 30 lines. Moved, not rewritten: the behaviour is the one `DocumentQuestionServiceTests` already checks
- [ ] T010 [P] Create `src/Scriptorium.Core/Services/AskFailureMapper.cs`: static `FromRouting(RoutingFailureKind, string)` and `FromProvider(AiFailureKind, string)` returning `AskResult`, moved from today's private `ToRoutingFailure` and `ToFailure` of `DocumentQuestionService`, same behaviour and messages
- [ ] T011 [P] Create `src/Scriptorium.Core/Services/PromptDocumentBlock.cs`: static `Wrap(string text)` returning the fenced `<document>` block, with the neutralising of a closing tag moved out of `QuestionPromptBuilder`
- [ ] T012 Change `src/Scriptorium.Core/Services/DocumentQuestionService.cs` to call `ReadyDocumentText.LoadAsync` and `AskFailureMapper` and delete its private loader, its nested class and its two private mappers. The constructor, `AskAsync` and every message stay as they are (needs T009 and T010)
- [ ] T013 Change `src/Scriptorium.Core/Services/QuestionPromptBuilder.cs` to build the `<document>` block with `PromptDocumentBlock.Wrap` and delete its private `Neutralize`; the prompt it produces stays **byte for byte** the same (needs T011)
- [ ] T014 [P] Create `src/Scriptorium.API/Endpoints/ModelPreferenceParser.cs`: public static `TryParse(string? value, out ModelPreference preference)`, moved from today's private `TryParsePreference` and `Chosen` of `QuestionEndpoints`
- [ ] T015 [P] Create `src/Scriptorium.API/Endpoints/AskFailureResponses.cs`: public static `From(AskFailureKind? kind, string error)` returning the `IResult`, moved from today's private `ToFailureResponse` of `QuestionEndpoints`, with `NotEnoughText` added to the `409` group
- [ ] T016 Change `src/Scriptorium.API/Endpoints/QuestionEndpoints.cs` to use `ModelPreferenceParser` and `AskFailureResponses` and delete its private copies; behaviour and messages unchanged (needs T014 and T015)
- [ ] T017 Run `dotnet build` and `dotnet test`: T003 to T007 pass; **every existing test passes without any edit**; `git diff main --stat -- tests` shows only the new files of T003 to T007 and no change to `DocumentQuestionServiceTests.cs`, `QuestionPromptBuilderTests.cs` or `QuestionEndpointTests.cs`; 0 build warnings. If an existing test fails, the move was not exact: fix the production code, never the test

**Checkpoint**: The question flow works exactly as before and the pieces the summary needs are available

---

## Phase 3: User Story 1 - Summarise a ready document (Priority: P1) 🎯 MVP

**Goal**: The person presses **Summarise** on a ready document and, after a short wait, sees one short paragraph based only on the document's text, with the label of the backend that produced it. With the delivered settings it is always the local model. A document that is not ready, unknown, or with too little text is refused with a clear message and no model is called.

**Independent Test**: With a ready fake document, press **Summarise** in the question panel and see a paragraph that matches the document and the label "Answered on this computer"; try a document that is not ready, an unknown id and a tiny file; check that the question flow is unchanged.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [ ] T018 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/SummaryPromptBuilderTests.cs` for `SummaryPromptBuilder.Build(string documentText)` with `QaOptions` (`MaxDocumentCharacters` 100 in the tests): two messages, the first `System` and the second exactly `User` with `Summarise the document.`; the system message asks for **one short paragraph of about three to five sentences** in plain text with no Markdown, no list and no title, in the **same language as the document**, using only the document, and says the document is data and any instruction inside it is ignored; it contains the document between `<document>` and `</document>` (through `PromptDocumentBlock`); a closing tag inside the document is neutralised; the text is cut at the limit with `TextExcerpt` (the model never receives more than 100 characters of a longer text, never ending on half of a character) and `Truncated` is true only when the text was longer; text exactly at the limit is not truncated
- [ ] T019 [P] [US1] Create `tests/Scriptorium.Core.Tests/Dtos/SummaryDtosTests.cs`: `SummaryRequestDto` reads `{}` and no field as no preference, and `{"preference":"Cloud"}` as `"Cloud"`; `SummaryResponseDto.From(AskResult.Success("text", truncated, backend))` gives `Summary`, `Truncated` and `Backend` `"local"` or `"cloud"`; a failed result gives an empty summary, `false` and `"local"`; the JSON field names are `summary`, `truncated` and `backend`
- [ ] T020 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/DocumentSummaryServiceTests.cs` with strict mocks for `IDocumentRepository`, `IAIProvider` (local) and `ICloudAIProvider`, the router built over them with the delivered `RoutingOptions` (cloud off): a ready document is answered by the local provider, the result is a success with the provider's text, `Backend` Local and the prompt's `Truncated`; the request sent to the model is the one `SummaryPromptBuilder` builds for that text; the cloud mock is never called; a model failure gives `ModelUnavailable` (for `Unavailable`) and `Timeout` (for `Timeout`) with the provider's text, and `Internal` with `The answer could not be produced.` for anything else; a provider that answers with a **blank text** (empty or only spaces) gives `Internal` with `The answer could not be produced.`, never an empty summary; the cancellation token reaches the provider; **order of checks**: an unknown document gives `NotFound`, a document not ready gives `NotReady` (text not read), and in both cases no model is called; **too little text**: a trimmed text of 49 characters gives `NotEnoughText` with `The document has too little text to summarise.` and **no model and no router are used**, 50 characters is summarised, and 100 spaces around 10 characters count as 10
- [ ] T021 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/SummaryEndpointTests.cs` in the style of `QuestionEndpointTests` (same `ApiTestHost`, `IAIProvider` replaced by a mock, no Ollama needed): `POST /api/documents/{id}/summary` with **no body** on a ready document gives `200` with `summary` equal to the mocked text, `truncated` false and `backend` `local`; the request the mock receives contains the document text; an unknown id gives `404` `Document not found.`; a failed document gives `409` `The document is not ready. Current status: failed.`; a text of 49 characters gives `409` `The document has too little text to summarise.`; in the last three cases the mock is never called; a model failure gives `503` / `504` with the provider's text and `500` with no detail; a request to summarise changes nothing (the document list and the extracted text are the same before and after)
- [ ] T022 [P] [US1] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`: `summarize('abc')` posts to `/api/documents/abc/summary` with the body `{ preference: 'auto' }` and returns the `SummaryResponse` (`summary`, `truncated`, `backend`); an error response is propagated with its status and message
- [ ] T023 [P] [US1] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (a new `describe` block, the existing specs stay as they are): the panel offers a **Summarise** button when a document is open (documents that are not ready never get the panel, because the **Ask** button exists only for ready ones); pressing it calls `summarize` with that document's id and the selector's value (`'auto'` when untouched, so the call is `summarize(id, 'auto')`) and shows a card titled `Summary` with the text and the label `Answered on this computer` (or `Answered by the cloud model` for `backend: 'cloud'`); while it works the progress indicator shows and both **Send** and **Summarise** are disabled; the summary is **not** added to the history sent with the next question, and a question still works while a summary is shown; opening another document, closing the panel and **New conversation** remove the summary and drop a late result; a failure goes through `describeAskFailure` and is shown in the same alert as a failed question, leaving no card: a `409` or `503` shows the server's text, a `404` shows `This document no longer exists. Close this panel and choose another one.` and a `500` shows the generic text; a second press while it works does nothing

### Implementation for User Story 1

- [ ] T024 [US1] Create `src/Scriptorium.Core/Ai/SummaryPrompt.cs`: `public sealed record SummaryPrompt(AiRequest Request, bool Truncated)`
- [ ] T025 [US1] Create `src/Scriptorium.Core/Services/SummaryPromptBuilder.cs` (constructor takes `QaOptions`): `Build(string documentText)` returns the `SummaryPrompt` described in T018 (instructions of research R3, the document through `PromptDocumentBlock.Wrap`, the cut with `TextExcerpt.Of`). Every method under 30 lines
- [ ] T026 [US1] Create `src/Scriptorium.Core/Dtos/SummaryDtos.cs`: `SummaryRequestDto(string? Preference = null)` and `SummaryResponseDto(string Summary, bool Truncated, string Backend = "local")` with `From(AskResult)` mapping the backend to `local` or `cloud` as `AskResponseDto` does
- [ ] T027 [US1] Create `src/Scriptorium.Core/Services/DocumentSummaryService.cs`: constructor `(IDocumentRepository, ModelRouter, ComplexityScorer, SummaryPromptBuilder)`; `SummarizeAsync(Guid documentId, ModelPreference preference, CancellationToken ct)` returns `AskResult`: load with `ReadyDocumentText.LoadAsync`; **trimmed text shorter than 50 characters** (a named constant `MinimumCharacters`) gives `NotEnoughText` before anything else is asked of a model; score with `_scorer.Score(string.Empty, text.Length, 0)` (research R2); `_router.Route(new RoutingRequest(isPrivate, preference, score.Value))`; on refusal `AskFailureMapper.FromRouting`; otherwise build the prompt, call the provider the router returned and map a failure with `AskFailureMapper.FromProvider`; a successful answer whose text is blank gives `Internal` with `The answer could not be produced.`; otherwise return `AskResult.Success(text, prompt.Truncated, backend)`. No Ollama or cloud class is named here. Every method under 30 lines
- [ ] T028 [US1] Create `src/Scriptorium.API/Endpoints/SummaryEndpoints.cs`: `MapSummaryEndpoints` maps `POST /api/documents/{id:guid}/summary` taking an optional `SummaryRequestDto? body` and the `DocumentSummaryService`; for now it passes `ModelPreference.Auto` (a comment says the next story reads the request's preference) and answers `Results.Ok(SummaryResponseDto.From(result))` or `AskFailureResponses.From(result.FailureKind, result.Error)`
- [ ] T029 [US1] In `src/Scriptorium.API/Program.cs` register `SummaryPromptBuilder` and `DocumentSummaryService` (singletons, like the question services) and call `MapSummaryEndpoints()` next to `MapQuestionEndpoints()`
- [ ] T030 [US1] Run `dotnet build` and `dotnet test`: T018 to T021 pass and everything else is still green, 0 warnings
- [ ] T031 [P] [US1] Update `src/Scriptorium.Web/src/app/documents/document.models.ts` (`SummaryResponse { summary: string; truncated: boolean; backend: 'local' | 'cloud' }`) and `document-api.service.ts` (`summarize(id: string, preference: AskPreference = 'auto'): Observable<SummaryResponse>` posting `{ preference }` to `/api/documents/{id}/summary`)
- [ ] T032 [US1] Update `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html`: a **Summarise** button next to **Send**; `summary` and `summarizing` state (reuse `waiting` and the existing `pending` subscription so a late result is dropped); a card with the heading `Summary`, the paragraph and the backend label (reuse `backendLabel`); `reset()` clears the summary; Send and Summarise disabled while working; the summary is not part of the transcript or the history. It calls `summarize(document.id, this.preference())` from the start, so the page already sends the selector's value; until User Story 2 the server ignores it. A failure is turned into text with the existing `describeAskFailure`. Add the few styles needed to `src/Scriptorium.Web/src/styles.css` in the existing style (light and dark). The component shows and decides nothing else
- [ ] T033 [US1] Run in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T022 and T023 pass, everything else is still green and no new warning (compare with T001)

### Manual check for User Story 1

- [ ] T034 [US1] With the user: throwaway API (quickstart prerequisites) and the fake files of T002; run quickstart scenarios 1, 2, 4 and 7 (endpoint by `curl`; Ollama needed) and the page part of scenario 6 (button, progress, card, label, reset, **Send** still works). Here the server ignores the selector (User Story 2) and a long document shows no notice yet (User Story 3). **Wait for the user's OK before anything is committed**; record the outcome in `specs/006-summaries/validation-results.md` (new file for this feature)

**Checkpoint**: A ready document can be summarised on this computer, and the question flow is unchanged

---

## Phase 4: User Story 2 - The summary follows the same model rules as the questions (Priority: P2)

**Goal**: The summary uses the person's preference (Auto, Local, Cloud) and the router's rules exactly as a question does: a private document is only summarised locally, Cloud while the cloud is off or without a key gives the explicit error, Cloud for a private document is refused, no fallback when the cloud fails.

**Independent Test**: For a private and a non-private document ask for a summary with each preference and check the answers and refusals; then, with a stand-in cloud, check that the same rules hold with the cloud on.

### Tests for User Story 2 (write first, they must fail) ⚠️

- [ ] T035 [P] [US2] Extend `tests/Scriptorium.Core.Tests/Services/DocumentSummaryServiceTests.cs` with the routing cases: with the cloud off, Auto and Local are answered locally; Cloud gives `ModelUnavailable` with `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` and no model is called; a private document with Cloud gives `PreferenceNotAllowed` with `This document is private, so it can only be answered on this computer. Choose Auto or Local.` and no model is called; the checks of the document and of the text come **before** routing (an unknown document, a document not ready and a text under 50 characters keep their kinds with the preference Cloud). With a router built for the test with `CloudEnabled` true over a stand-in `ICloudAIProvider` that is configured: **with the default threshold (2) a long document under Auto stays local** (research R2, the score of a summary is 0 or 1); with `ComplexityThreshold` 0 a long document (text at 75 % of the limit or more) under Auto is answered by the stand-in with `Backend` Cloud, and a short one stays local; a private document under Auto or Local never reaches the stand-in; preference Cloud on a non-private document reaches it; with the cloud off the same cases all go local; a stand-in that answers `Unavailable` or `Timeout` gives `ModelUnavailable` or `Timeout` with its text and the local provider is **never called** (no fallback); and a **theory over the whole table** (SC-003): private yes/no × preference Auto/Local/Cloud × cloud state off / on and configured / on but not configured, with the score fixed by the document length, giving the same backend or failure kind that `ModelRouterTests` gives for a question
- [ ] T036 [P] [US2] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/SummaryEndpointTests.cs` with the cases of contracts/summary-api.md: `preference` absent, `null`, empty, `"auto"` and `"Local"` are answered by the mocked local provider; `"cloud"` with the delivered settings gives `503` with the not-available message and the local provider is **never** called; `"cloud"` on a private document gives `409` with the private message and no provider is called; `"turbo"` gives `400` with `The preference must be auto, local or cloud.` and is checked **before** an unknown id would give `404`; `"cloud"` on an unknown document, on a document not ready and on a tiny text keep their `404`, `409`, `409`. Cloud on by configuration (`Routing:CloudEnabled` true) with a stand-in `ICloudAIProvider` replacing `ClaudeProvider` (as the existing tests replace `IAIProvider`): `"cloud"` gives `200` with `"backend":"cloud"` and the stand-in's text; the same on a private document gives `409`; Auto and Local on a private document give `"local"`; a stand-in that answers `Unavailable` gives `503` and `Timeout` gives `504` with its own text, and the local provider is never called; with cloud on but the **real** `ClaudeProvider` and the key option set to empty (`UseSetting("Anthropic:ApiKey", "")`), `"cloud"` gives `503` with the not-available message and Auto is answered locally. No test registers a key or reaches the network
- [ ] T037 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`: `summarize('abc', 'cloud')` sends `{ preference: 'cloud' }`
- [ ] T038 [P] [US2] Extend the `describe` block of `ask-panel.component.spec.ts` for summaries: **Summarise** sends the preference chosen in the **Answer with** selector (`auto` by default, `local`, `cloud`); the Cloud choice is not disabled; a `503` and a `409` response show the server's message as it is and leave no card; the selector goes back to Auto when another document is opened, when the panel is closed and on **New conversation**, whether or not a summary was asked for. Existing specs stay as they are

### Implementation for User Story 2

- [ ] T039 [US2] In `src/Scriptorium.API/Endpoints/SummaryEndpoints.cs` read `body?.Preference` with `ModelPreferenceParser.TryParse` **before anything else** (an invalid value is `400` with `The preference must be auto, local or cloud.`) and pass the result to `SummarizeAsync` instead of the fixed `ModelPreference.Auto`. `DocumentSummaryService` already routes with the preference it is given: no change expected there. Every method under 30 lines
- [ ] T040 [US2] Run `dotnet build` and `dotnet test`: T035 and T036 pass and everything else is still green. If a router or service test fails, it shows a gap: fix the production code (never weaken the test) and tell the user what was found
- [ ] T041 [US2] Check `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts`: **Summarise** already passes `this.preference()` since T032, so no code change is expected; if T038 shows otherwise, fix the component (it sends the preference and shows `backend`; it decides nothing)
- [ ] T042 [US2] Run in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T037 and T038 pass, everything else is still green and no new warning

### Manual check for User Story 2

- [ ] T043 [US2] With the user: run quickstart scenario 3 (Cloud on the normal and on the private document, and an invalid preference, by `curl`; then the same on the page with the selector) and scenario 8 (restart the throwaway API with `Routing__CloudEnabled=true` and **no key**: Cloud still gives the not-available message and Auto answers locally). Never use a real key. **Wait for the user's OK before anything is committed**; record the outcome in `specs/006-summaries/validation-results.md`

**Checkpoint**: A summary obeys the same preference and privacy rules as a question, and the cloud is still off and unused

---

## Phase 5: User Story 3 - A long document says what the summary covers (Priority: P3)

**Goal**: A summary of a document longer than the limit the model reads says that it is based only on the first part; a shorter one shows no notice.

**Independent Test**: Summarise a document longer than 8,000 characters and one shorter, and check that only the first shows the notice.

### Tests for User Story 3 (write first, they must fail) ⚠️

- [ ] T044 [P] [US3] Extend `tests/Scriptorium.Core.Tests/Services/DocumentSummaryServiceTests.cs`: a text longer than the limit gives a success with `Truncated` true and the model receives only the first `MaxDocumentCharacters` characters of it (the end of the text is not in the request); a text at or under the limit gives `Truncated` false; the cut never ends on half of a character
- [ ] T045 [P] [US3] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/SummaryEndpointTests.cs`: a document of 9,000 characters (default limit 8,000) gives `"truncated": true` and the request the mock receives does not contain the last characters of the text; a short one gives `"truncated": false`
- [ ] T046 [P] [US3] Extend the `describe` block of `ask-panel.component.spec.ts` for summaries: when `truncated` is true the card shows a note with `This summary is based only on the first part of the document, which is too long to be read in full.`; when false there is no note; the note is not shown for a failed request

### Implementation for User Story 3

- [ ] T047 [US3] In `ask-panel.component.html` show the note of T046 in the summary card when the response says `truncated`, with the same `notice` style the answers use. The cut itself already exists (`SummaryPromptBuilder` and the DTO carry `Truncated` since User Story 1)
- [ ] T048 [US3] Run `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T044 to T046 pass and everything else is still green. If T044 or T045 fail, the cut is wrong: fix the production code and tell the user

### Manual check for User Story 3

- [ ] T049 [US3] With the user: run quickstart scenario 5 (`long.txt` gives `"truncated": true`, `facts.txt` does not) and the notice on the page (scenario 6, the notice part). **Wait for the user's OK before anything is committed**; record the outcome in `specs/006-summaries/validation-results.md`

**Checkpoint**: All three stories work; the summary never pretends to cover a text it did not read

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, review against the constitution, the final regression run, and the pull request

- [ ] T050 [P] Update `README.md`: the roadmap row "Summaries" becomes done; a line in "What it does today" about **Summarise** (one short paragraph, from the first part of the text, produced each time, nothing stored, same model choice as the questions, with the label); the API table gets `POST /api/documents/{id}/summary` (optional `preference`; `200` with `summary`, `truncated` and `backend`; `400`, `404`, `409` for not ready, too little text or Cloud on a private document, `503`, `504`); link the feature's contract and spec folder
- [ ] T051 [P] Update `docs/architecture.md`: a new flow **4.4 Summarising a document** (a sequence diagram: page, endpoint, `DocumentSummaryService`, `ReadyDocumentText`, scorer, router, `SummaryPromptBuilder`, provider; the refusals; nothing stored); the helpers (`ReadyDocumentText`, `AskFailureMapper`, `PromptDocumentBlock`, `ModelPreferenceParser`, `AskFailureResponses`) in the layers table and in the question flow's text; section 7 says how Auto scores a summary (empty question, score 0 or 1, so local with the default threshold); the web table mentions the **Summarise** button
- [ ] T052 Review the new and changed code against the constitution: every class has one responsibility and every method is under 30 lines (`DocumentSummaryService.SummarizeAsync` in particular); no `!` without a comment, no `.Result`/`.Wait()`, no `dynamic` or reflection; **`ModelRouter` is still the only place that chooses between local and cloud** (`git grep` for other decisions on `ModelBackend` or `CloudEnabled` outside the router and the wiring); no `HttpClient` outside `OllamaProvider` and `ClaudeProvider`; no Ollama and no cloud code in the new classes; `DocumentQuestionService` is smaller than before and its public shape is unchanged. Run `git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'`: only code that reads the key option, no key value anywhere. Confirm with `git diff main --name-only` that **no `.csproj`, `package.json` or `package-lock.json` changed**, and that no migration or entity was added. List any deviation and ask the user before fixing anything beyond the feature
- [ ] T053 Run `dotnet build`, the full `dotnet test`, and in `src/Scriptorium.Web` the full `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`; compare with the T001 baseline (no test removed, only additions, no new warnings); run quickstart scenarios 9 and 10; complete `specs/006-summaries/validation-results.md` with the results of scenarios 1 to 10, saying plainly that the real cloud service was never called and that the quality of the summaries was judged by reading them
- [ ] T054 Hand over to the user: summarise the work and the validation. **Only with the user's OK**: commit what is not yet committed (messages in English, descriptive, with the required `Co-Authored-By` line), push `008-summaries`, and open **one** pull request to `main` for the whole feature; merge it only when the user says so, then update `main` and delete the branch locally and on the remote. Never commit on `main`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **Foundational (Phase 2)**: after Setup; blocks all three stories
- **User Story 1 (Phase 3)**: after Foundational
- **User Story 2 (Phase 4)**: after User Story 1, because it changes the same endpoint file and the same panel files and replaces the fixed `Auto` of T028
- **User Story 3 (Phase 5)**: after User Story 2 (same test files and the same card); it adds mostly tests and one note in the page
- **Polish (Phase 6)**: after all three stories

### Within Each Story

- Foundational: tests T003 to T007 first; T008 first among the code (T015 and T020 use `NotEnoughText`); T009 before T012; T010 before T012; T011 before T013; T014 and T015 before T016; T017 closes the phase
- Story 1: T018 to T023 first; T024 before T025 (the builder returns the record); T025 and T026 before T027 and T028; T027 before T028; T028 before T029; T030 closes the backend; T031 before T032; T033 closes the frontend, then the manual check
- Story 2: T035 to T038 first; T039 before T040; T041 is a check, after T039 and before T042; the manual check last
- Story 3: T044 to T046 first, T047, T048, then the manual check
- The manual checks (T034, T043, T049) come before any commit of the story

### Parallel Opportunities

- T002 can be done at any time in Phase 1
- T003 to T007 are in different files and can be written together; T010, T011, T014 and T015 are different files and can be written together after T008
- T018 to T023 are six different files (T020 and T021 are new files; T022 and T023 extend two existing specs)
- T035 to T038 extend four different files; T044 to T046 extend three different files
- T050 and T051 are in different files
- **Not** parallel: T012, T013, T016 (they change production files that other tasks read); T027, T028, T032 and T039 (same service, endpoint and component files as the tasks before them); T020, T035, T044 (same test file in different phases) and T021, T036, T045 (same)

### Parallel Example: foundation tests

```text
# Write the five helper test files together before any production code:
Task: "T003 ReadyDocumentTextTests in tests/Scriptorium.Core.Tests/Services/ReadyDocumentTextTests.cs"
Task: "T004 AskFailureMapperTests in tests/Scriptorium.Core.Tests/Services/AskFailureMapperTests.cs"
Task: "T005 PromptDocumentBlockTests in tests/Scriptorium.Core.Tests/Services/PromptDocumentBlockTests.cs"
Task: "T006 ModelPreferenceParserTests in tests/Scriptorium.Infrastructure.Tests/Endpoints/ModelPreferenceParserTests.cs"
Task: "T007 AskFailureResponsesTests in tests/Scriptorium.Infrastructure.Tests/Endpoints/AskFailureResponsesTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 and T002: baseline and fake files
2. Foundational: T003 to T017 (the extractions, with no change to any existing test)
3. T018 to T033: a summary of a ready document, end to end, on the local model
4. T034: try it with the user
5. **Stop and validate**: the user tries scenarios 1, 2, 4, 6 and 7 and gives the OK; only then commit (on `008-summaries`). Do not open the pull request yet: the preference and the long-document notice come with User Stories 2 and 3

### Incremental Delivery

1. Foundation + User Story 1 → tried by the user → commit
2. User Story 2 (the preference, the refusals, the stand-in cloud proof) → tried by the user → commit
3. User Story 3 (the notice for a long document) → tried by the user → commit
4. Polish: README, architecture document, review, final regression run → commit → one pull request for the whole feature

---

## Notes

- [P] tasks = different files, no dependencies
- The decision lives only in `ModelRouter`; the page sends the preference and shows `backend`
- `ReadyDocumentText`, `AskFailureMapper`, `PromptDocumentBlock`, `ModelPreferenceParser` and `AskFailureResponses` are **moved**, not rewritten: the existing tests of the question flow are the proof, and they are not edited
- `AskFailureKind.NotEnoughText` is added at the end of the enum so the existing values keep their numbers
- Auto scores a summary with the existing scorer, an empty question and no earlier exchanges (decided with the user): the score is 0 or 1, so with the default threshold a summary stays local under Auto
- Nothing is stored: no entity, no column, no migration, no cache
- The cloud stays **off** in every manual step; no key is set, no key is asked for, and the real cloud service is never called
- Fake files only: no document uploaded by the user is read or used for tests or diagnosis
- If a task shows that the framework behaves differently from research.md, stop and update research.md before going on, and tell the user
