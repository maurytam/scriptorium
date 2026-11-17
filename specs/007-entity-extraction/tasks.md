---

description: "Task list for Entity Extraction"

---

# Tasks: Entity Extraction

**Input**: Design documents from `/specs/007-entity-extraction/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/entities-api.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity). Backend: xUnit + FluentAssertions + Moq, plus endpoint tests on the in-process `ApiTestHost`. Frontend: Jasmine/Karma specs next to each component and service. Tests are written before the code they cover and must fail first (a test that does not compile yet counts as failing). All test documents are fake files; no document uploaded by the user is read or used. **No test and no manual step calls the real cloud service or needs a key**: the cloud-on cases use a stand-in `ICloudAIProvider`.

**Organization**: Tasks are grouped by user story (from spec.md). The shared half of `DocumentSummaryService` is moved into `RoutedDocument` in Foundational: it changes **no behaviour** and every existing test keeps its assertions and its set-up (`DocumentSummaryServiceTests` and `SummaryEndpointTests` are not edited at all). User Story 1 builds an extraction end to end with the local model (prompt, reply parser, verifier, service, DTOs, endpoint, page). User Story 2 adds the preference and the refusals (the router rules of feature 005, proved also with a stand-in cloud). User Story 3 adds the notice that says only the first part of a long document was read. **No NuGet package, no npm package, no migration, nothing stored, no change to `AiRequest` or to either provider.**

**Commits**: Nothing is committed until the user has tried the story with the project's manual check and given the OK (project rule). Work stays on branch `009-entity-extraction`. There is **one** pull request for the whole feature, opened only after the user's OK, never on `main` directly; it is merged only when the user says so, then the branch is deleted locally and on the remote.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2, US3)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/src/app/documents/`, `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/` (it also holds the endpoint tests). Manual-test files go in `~/Desktop/prove-scriptorium` (fake content only). Manual checks run on a throwaway API with its own database in the scratchpad, on port 5099, and, for the page, on a **separate** `ng serve` on port 4299 whose proxy file (kept in the scratchpad, outside the repository) points to 5099, so the user's own API (5034) and page (4200) are never touched. Each is stopped only by the PID of its own port, never with `pkill`, and the user's real database and documents are never used. **After uploading a fake file, list the documents and check `isPrivate`** (once an upload silently lost the flag).

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" can be checked at the end, and have the fake files

- [X] T001 Check that the current branch is `009-entity-extraction`, then run `dotnet build` and `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`, before any change; note the build warnings and the number of passing tests per test project (backend: Core and Infrastructure; frontend: Karma). Expected: Core 373, Infrastructure 284, frontend 172, 0 warnings. If the frontend tests cannot run (no Chrome, missing `node_modules`), say so to the user before going on (reference for T028, T037, T047)
- [X] T002 [P] Check that the fake files `facts.txt` (two people, two organizations, two dates, one amount, three places), `router-private.txt`, `long.txt` (longer than 8,000 characters), `tiny.txt` and `broken.docx` exist in `~/Desktop/prove-scriptorium`; create nothing else and do not read or use any other file of the user. Nothing to create if all five exist

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Move out of `DocumentSummaryService` what the extraction needs too, so nothing is copied. **Nothing here changes what the user sees**, and `DocumentSummaryServiceTests`, `SummaryEndpointTests`, `DocumentQuestionServiceTests`, `QuestionEndpointTests` are not edited at all (they are the proof that the move is exact).

**⚠️ CRITICAL**: No story work can start until this is complete

### Tests for the shared piece (write first, they must fail) ⚠️

- [X] T003 [P] Create `tests/Scriptorium.Core.Tests/Services/RoutedDocumentTests.cs` for `RoutedDocument` with strict mocks for `IDocumentRepository`, `IAIProvider` and `ICloudAIProvider` and a real `ModelRouter` and `ComplexityScorer` (`QaOptions` with `MaxDocumentCharacters` 100): **`PrepareAsync(repository, router, scorer, documentId, preference, tooLittleTextMessage, ct)`** returns an object that is itself the result (`IsSuccess`, `Text`, a failure as an `AskResult`): an unknown document gives `NotFound` and a document not ready gives `NotReady` (the text is **not** read) and no model is called; a trimmed text of 49 characters gives `NotEnoughText` with **the message passed in** and no routing; 50 characters pass; 100 spaces around 10 characters count as 10; with the delivered `RoutingOptions` Auto and Local pick the local provider; Cloud with the cloud off gives `ModelUnavailable` with `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.`; a private document with Cloud gives `PreferenceNotAllowed` with `This document is private, so it can only be answered on this computer. Choose Auto or Local.`; the checks of the document and of the text come **before** routing (all three keep their kind with the preference Cloud); with `CloudEnabled` true over a configured stand-in and the default threshold (2) a long text (80 characters, 75 % of the limit) under Auto stays local, with `ComplexityThreshold` 0 it goes to the stand-in and a short one (60 characters) stays local (the score is that of an empty question with 0 exchanges); a private document never reaches the stand-in; **`CompleteAsync(request, truncated, ct)`** on a prepared document calls the chosen provider only and returns an `AskResult` with the text, the `Truncated` flag passed in and the `Backend`; `Unavailable` and `Timeout` give `ModelUnavailable` and `Timeout` with the provider's text and anything else gives `Internal` with `The answer could not be produced.` and **no detail**; a blank reply (empty or only spaces) gives the same `Internal`; a failing stand-in is reported and the local provider is **never called** (no fallback); the cancellation token reaches the repository and the provider

### Implementation for the foundation

- [X] T004 Create `src/Scriptorium.Core/Services/RoutedDocument.cs`: a sealed class that is its own result, with `IsSuccess` (with `MemberNotNullWhen`), the document's text, the failure as an `AskResult`, and a public const `MinimumCharacters = 50` (moved from `DocumentSummaryService`); a static `PrepareAsync(IDocumentRepository repository, ModelRouter router, ComplexityScorer scorer, Guid documentId, ModelPreference preference, string tooLittleTextMessage, CancellationToken ct)` that does, **in this order**, `ReadyDocumentText.LoadAsync`, the minimum of `MinimumCharacters` on the trimmed text (`NotEnoughText` with the message passed in), `scorer.Score(string.Empty, text.Length, 0)` (research R5), `router.Route(new RoutingRequest(isPrivate, preference, score.Value))` and `AskFailureMapper.FromRouting` on a refusal; an instance `CompleteAsync(AiRequest request, bool truncated, CancellationToken ct)` that calls the provider the router returned, maps a failure with `AskFailureMapper.FromProvider`, turns a blank reply into `Internal` with `The answer could not be produced.` and otherwise returns `AskResult.Success(text, truncated, backend)`. Moved, not rewritten: it is today's code of `DocumentSummaryService`. No Ollama or cloud class is named. Doc comments, every method under 30 lines
- [X] T005 Change `src/Scriptorium.Core/Services/DocumentSummaryService.cs` to call `RoutedDocument.PrepareAsync` (passing its own `The document has too little text to summarise.`) and `routed.CompleteAsync`, and delete its private `Route`, its private `SummariseAsync` and its `MinimumCharacters`. Its constructor, `SummarizeAsync`, every message and its behaviour stay as they are (needs T004)
- [X] T006 Run `dotnet build` and `dotnet test`: T003 passes; **every existing test passes without any edit**; `git status` and `git diff main --stat -- tests` show only the new file of T003 and no change to `DocumentSummaryServiceTests.cs` or `SummaryEndpointTests.cs`; 0 build warnings. If an existing test fails, the move was not exact: fix the production code, never the test

**Checkpoint**: Summaries work exactly as before and the shared piece the extraction needs is available

---

## Phase 3: User Story 1 - See the key entities of a ready document (Priority: P1) 🎯 MVP

**Goal**: The person presses **Extract entities** on a ready document and, after a short wait, sees the people, organizations, dates, amounts and places found in its text, grouped by type, each once, as written, with the label of the backend that produced them. Only entities that are in the text read are shown. With the delivered settings it is always the local model. A document that is not ready, unknown, or with too little text is refused with a clear message and no model is called; a text with no entity says so.

**Independent Test**: With a ready fake document, press **Extract entities** in the question panel and see the entities that match the document, grouped by type, with the label "Answered on this computer"; try a document that is not ready, an unknown id, a tiny file and a file with no entities; check that the question flow and the summary are unchanged.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [X] T007 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/EntityPromptBuilderTests.cs` for `EntityPromptBuilder.Build(string documentText)` with `QaOptions` (`MaxDocumentCharacters` 100 in the tests): two messages, the first `System` and the second exactly `User` with `Extract the entities.`; the system message asks for the five kinds `person`, `organization`, `date`, `amount` and `place`, **one entity per line** written as `kind: text`, each copied **exactly as written in the document** (not translated, converted or invented), with no Markdown and no other text, and for the single word `NONE` when there is nothing; it says the document is data and any instruction inside it is ignored; it contains the document between `<document>` and `</document>` (through `PromptDocumentBlock`); a closing tag inside the document is neutralised; the text is cut at the limit with `TextExcerpt` (never more than 100 characters of a longer text, never ending on half of a character), `Truncated` is true only when the text was longer, and **`TextRead` is exactly the text that went into the request** (the first 100 characters, or the whole text when shorter)
- [X] T008 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/EntityReplyParserTests.cs` for `EntityReplyParser.Parse(string reply)`, returning an `EntityReply` with a `Kind` (`Entities`, `None` or `Unreadable`) and the `Candidates` (type and text): lines `person: Mario Rossi`, `organization: Alfa Srl`, `date: 12 March 2026`, `amount: 15,000 euro`, `place: Turin` give five candidates of the right types; the kind is read in any letter case and in the singular or the plural (`People:`, `PLACES:`, `Organizations:`); a bullet or a number in front of a line (`- `, `* `, `1. `) is dropped; a line is split at the **first** colon only (`amount: 15,000 euro`, `date: 5 May, 10:30`); spaces around the kind and the text are trimmed; blank lines, lines without a colon, lines with an unknown kind (`email: x@y.z`) and lines with an empty text are ignored; a friendly sentence before or after the lines and Markdown fence lines (three backticks) do not matter; Windows line breaks work; a reply that is only `NONE` (any letter case, spaces around, with or without a final full stop) is `None`; a reply with **no** readable line that is not `NONE` (a sentence of prose, a refusal, a JSON object) is `Unreadable`; a mix of readable and unreadable lines is `Entities` with only the readable ones; an empty reply is `Unreadable`
- [X] T009 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/EntityVerifierTests.cs` for `EntityVerifier.Verify(IReadOnlyList<EntityCandidate> candidates, string textRead)`, returning the checked `EntityGroup` list: an entity found in the text is kept and one that is not in the text is dropped (an invented person, an invented date, an invented amount); the comparison ignores letter case and counts any run of spaces and line breaks as one space (an entity `Alfa Srl` is found in `alfa\n  srl`); the entity is shown **as the text writes it** (the model's `mario rossi` becomes `Mario Rossi`), with a run of spaces or line breaks inside it shown as **one space** (the text `Alfa\n   Srl` is shown `Alfa Srl`, never with a line break); the same entity written twice, or in another letter case or with other spacing, appears once per type; the same text in two types is kept in both; the groups come in the fixed order People, Organizations, Dates, Amounts, Places and only the non-empty ones are returned; inside a group the entities are ordered by **where they first appear in the text**, not by the model's order; no candidates, or all of them dropped, gives an empty list; a candidate of only spaces is ignored; accented letters (`Perù`, `Città di Milano`) are compared like the rest; a name that is only a part of a longer word in the text (`Rossi` in `Rossini`) is accepted (research R3)
- [X] T010 [P] [US1] Create `tests/Scriptorium.Core.Tests/Results/EntityResultTests.cs` and `tests/Scriptorium.Core.Tests/Dtos/EntityDtosTests.cs`: `EntityResult.Success(groups, truncated, backend)` is a success carrying them (an empty list is a success) and `EntityResult.Failure(kind, error)` is a failure with a kind and a message and no groups; `EntityRequestDto` reads `{}` and no field as no preference and `{"preference":"Cloud"}` as `"Cloud"`; `EntityResponseDto.From(result)` maps the groups to `people`, `organizations`, `dates`, `amounts` and `places`, the entities in order, `Truncated` and `Backend` (`local` or `cloud`); a failed result gives no groups, `false` and `local`; the JSON field names are `groups`, `type`, `entities`, `truncated` and `backend`
- [X] T011 [P] [US1] Create `tests/Scriptorium.Core.Tests/Services/DocumentEntityServiceTests.cs` with strict mocks for `IDocumentRepository`, `IAIProvider` and `ICloudAIProvider`, the router built over them with the delivered `RoutingOptions` (cloud off): a ready document is answered by the local provider and the reply lines become groups, with `Backend` Local and the prompt's `Truncated`; the request sent to the model is the one `EntityPromptBuilder` builds for that text; the cloud mock is never called; a reply of `NONE` is a **success with no groups**; an entity the model invented is not in the result; a reply whose entities are all invented is a success with no groups; a reply with no readable line (prose) gives `Internal` with `The entities could not be extracted.` and **no part of the model's text**; a blank reply gives `Internal` with `The answer could not be produced.`; a model failure gives `ModelUnavailable` (for `Unavailable`) and `Timeout` (for `Timeout`) with the provider's text and `Internal` with `The answer could not be produced.` for anything else; the cancellation token reaches the provider; **order of checks**: an unknown document gives `NotFound`, a document not ready gives `NotReady` (text not read) and in both cases no model is called; a trimmed text of 49 characters gives `NotEnoughText` with `The document has too little text to extract entities from.` and no model, 50 characters are processed
- [X] T012 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/EntityEndpointTests.cs` in the style of `SummaryEndpointTests` (same `ApiTestHost`, `IAIProvider` replaced by a mock that answers with entity lines, no Ollama needed): `POST /api/documents/{id}/entities` with **no body** on a ready document gives `200` with `groups` in the fixed order holding the entities of the mocked reply that are in the document, `truncated` false and `backend` `local`; the request the mock receives contains the document text; a reply of `NONE` gives `200` with `"groups": []`; an entity not in the document is absent from the response; a reply with no readable line gives `500` `The entities could not be extracted.` and no detail of the reply; an unknown id gives `404` `Document not found.`; a failed document gives `409` `The document is not ready. Current status: failed.`; a text of 49 characters gives `409` `The document has too little text to extract entities from.`; in the last three cases the mock is never called; a model failure gives `503` / `504` with the provider's text and `500` `The answer could not be produced.` with no detail; a request changes nothing (the document list and the extracted text are the same before and after)
- [X] T013 [P] [US1] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`: `extractEntities('abc')` posts to `/api/documents/abc/entities` with the body `{ preference: 'auto' }` and returns the `EntitiesResponse` (`groups`, `truncated`, `backend`); an error response is propagated with its status and message
- [X] T014 [P] [US1] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (a new `describe` block, the existing specs stay as they are; the only change to the existing set-up is `'extractEntities'` in the list of methods of the API spy): the panel offers an **Extract entities** button next to **Summarise** when a document is open; pressing it calls `extractEntities` with that document's id and the selector's value (`'auto'` when untouched) and shows a card titled `Entities` with one block per **non-empty** type (headings `People`, `Organizations`, `Dates`, `Amounts`, `Places`, in the order received) listing its entities, **no heading for a type that is absent**, and the label `Answered on this computer` (or `Answered by the cloud model` for `backend: 'cloud'`); a response with no groups shows `No entities were found in this document.` and no heading; while it works the progress indicator shows and **Send, Summarise and Extract entities are all disabled**, and while a summary works Extract entities is disabled too; a second press while it works does nothing; the entities are **not** added to the history sent with the next question and a question still works while they are shown; **a summary and the entities can be shown together**, and asking again replaces only its own card; opening another document, closing the panel and **New conversation** remove the entities and drop a late result; **New conversation** is offered when only the entities are shown; a response with about 60 entities in one group shows all of them as a list, each one present; a failure goes through `describeAskFailure` and is shown in the same alert as a failed question, leaving no card (a `409` or `503` shows the server's text, a `404` shows `This document no longer exists. Close this panel and choose another one.`, a `500` shows the generic text)

### Implementation for User Story 1

- [X] T015 [P] [US1] Create `src/Scriptorium.Core/Enums/EntityType.cs`: `Person`, `Organization`, `Date`, `Amount`, `Place`, in this order (it is the order of display)
- [X] T016 [US1] Create `src/Scriptorium.Core/Ai/EntityPrompt.cs` (`public sealed record EntityPrompt(AiRequest Request, bool Truncated, string TextRead)`), `src/Scriptorium.Core/Ai/EntityCandidate.cs` (`EntityCandidate(EntityType Type, string Text)`) and `src/Scriptorium.Core/Results/EntityGroup.cs` (`EntityGroup(EntityType Type, IReadOnlyList<string> Entities)`) (needs T015)
- [X] T017 [US1] Create `src/Scriptorium.Core/Services/EntityPromptBuilder.cs` (constructor takes `QaOptions`): `Build(string documentText)` returns the `EntityPrompt` described in T007 (the instructions of research R2, the document through `PromptDocumentBlock.Wrap`, the cut with `TextExcerpt.Of`, `TextRead` = the cut text). Every method under 30 lines (needs T016)
- [X] T018 [US1] Create `src/Scriptorium.Core/Services/EntityReplyParser.cs` and `src/Scriptorium.Core/Services/EntityReply.cs` (`EntityReply` with `EntityReplyKind Kind` and the `Candidates`; the enum `EntityReplyKind { Entities, None, Unreadable }`): a static `Parse(string reply)` that does what T008 describes. A pure function, no exception for a strange reply, every method under 30 lines (needs T016)
- [X] T019 [US1] Create `src/Scriptorium.Core/Services/EntityVerifier.cs`: a static `Verify(IReadOnlyList<EntityCandidate> candidates, string textRead)` that does what T009 describes (normalise letter case and runs of white space for the comparison only; keep the text's own spelling; no repeats; order by first position; fixed order of the types; non-empty groups only). A pure function, every method under 30 lines (needs T016)
- [X] T020 [US1] Create `src/Scriptorium.Core/Results/EntityResult.cs`: `Success(IReadOnlyList<EntityGroup> groups, bool truncated, ModelBackend backend)` and `Failure(AskFailureKind kind, string error)`, with `IsSuccess` and `MemberNotNullWhen` in the style of `AskResult` (needs T016)
- [X] T021 [US1] Create `src/Scriptorium.Core/Dtos/EntityDtos.cs`: `EntityRequestDto(string? Preference = null)`, `EntityGroupDto(string Type, IReadOnlyList<string> Entities)` and `EntityResponseDto(IReadOnlyList<EntityGroupDto> Groups, bool Truncated, string Backend = "local")` with `From(EntityResult)` mapping the types to `people`, `organizations`, `dates`, `amounts` and `places` and the backend to `local` or `cloud` as `SummaryResponseDto` does (needs T020)
- [X] T022 [US1] Create `src/Scriptorium.Core/Services/DocumentEntityService.cs`: constructor `(IDocumentRepository, ModelRouter, ComplexityScorer, EntityPromptBuilder)`; `ExtractAsync(Guid documentId, ModelPreference preference, CancellationToken ct)` returns `EntityResult`: `RoutedDocument.PrepareAsync` (with `The document has too little text to extract entities from.`); on its failure `EntityResult.Failure`; build the prompt; `routed.CompleteAsync`; on its failure `EntityResult.Failure`; `EntityReplyParser.Parse` the reply: `Unreadable` gives `Internal` with `The entities could not be extracted.`, `None` gives a success with no groups, otherwise `EntityVerifier.Verify(candidates, prompt.TextRead)` and a success with the groups, `prompt.Truncated` and the backend. No Ollama or cloud class is named here. Every method under 30 lines (needs T004, T017, T018, T019, T020)
- [X] T023 [US1] Create `src/Scriptorium.API/Endpoints/EntityEndpoints.cs`: `MapEntityEndpoints` maps `POST /api/documents/{id:guid}/entities` taking an optional `EntityRequestDto? body` and the `DocumentEntityService`; for now it passes `ModelPreference.Auto` (a comment says the next story reads the request's preference) and answers `Results.Ok(EntityResponseDto.From(result))` or `AskFailureResponses.From(result.FailureKind, result.Error)`. In `src/Scriptorium.API/Program.cs` register `EntityPromptBuilder` and `DocumentEntityService` (singletons, like the summary services) and call `MapEntityEndpoints()` next to `MapSummaryEndpoints()` (needs T021, T022)
- [X] T024 [US1] Run `dotnet build` and `dotnet test`: T007 to T012 pass and everything else is still green, 0 warnings
- [X] T025 [P] [US1] Update `src/Scriptorium.Web/src/app/documents/document.models.ts` (`EntityType = 'people' | 'organizations' | 'dates' | 'amounts' | 'places'`, `EntityGroup { type: EntityType; entities: string[] }`, `EntitiesResponse { groups: EntityGroup[]; truncated: boolean; backend: 'local' | 'cloud' }`) and `document-api.service.ts` (`extractEntities(id: string, preference: AskPreference = 'auto'): Observable<EntitiesResponse>` posting `{ preference }` to `/api/documents/{id}/entities`)
- [X] T026 [US1] Update `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html`: an **Extract entities** button between **Summarise** and **Send**; an `entities` state next to `summary`; the two "ask the server and show the result" paths (summary and entities) go through **one** private method that handles `waiting`, `pending`, the error text (`describeAskFailure`) and the clearing of that path's own card, so the component stays small; a card with the heading `Entities`, one block per group with its heading (`People`, `Organizations`, `Dates`, `Amounts`, `Places`, a small label table in the component, nothing else decided) and the entities as a wrapping list, the backend label (reuse `backendLabel`) and, when there are no groups, `No entities were found in this document.`; `reset()` clears the entities; Send, Summarise and Extract entities disabled while any request works; the entities are not part of the transcript or the history; **New conversation** is also offered when only the entities are shown. It calls `extractEntities(document.id, this.preference())` from the start, so the page already sends the selector's value; until User Story 2 the server ignores it. Add the few styles needed to `src/Scriptorium.Web/src/styles.css` in the existing style (light and dark, wrapping, readable on a narrow screen). The component shows and decides nothing else (needs T025)
- [X] T027 [US1] Run in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T013 and T014 pass, everything else is still green (the summary specs included) and no new warning (compare with T001)

### Manual check for User Story 1

- [X] T028 [US1] With the user: throwaway API (quickstart prerequisites) and a separate page on 4299, the fake files of T002, `isPrivate` checked in the list after the upload; run quickstart scenarios 1, 2, 4 and 7 (endpoint by `curl`; Ollama needed) and the page part of scenario 6 (button, progress, card, headings only for the types found, label, "none found", reset, **Send** and **Summarise** still work, summary and entities together). Here the server ignores the selector (User Story 2) and a long document shows no notice yet (User Story 3). Read the entities against the files and say plainly what the model got right or wrong. Also look at a fake file with many names (about 40 people and places, made for the occasion) to see that the list stays readable and does not run past the screen, on a wide and on a narrow window, in the light and in the dark theme. **Wait for the user's OK before anything is committed**; record the outcome in `specs/007-entity-extraction/validation-results.md` (new file for this feature)

**Checkpoint**: A ready document's entities are shown on this computer, nothing invented, and the question and summary flows are unchanged

---

## Phase 4: User Story 2 - Extraction follows the same model rules as questions and summaries (Priority: P2)

**Goal**: The extraction uses the person's preference (Auto, Local, Cloud) and the router's rules exactly as a question and a summary do: a private document is only processed locally, Cloud while the cloud is off or without a key gives the explicit error, Cloud for a private document is refused, no fallback when the cloud fails.

**Independent Test**: For a private and a non-private document ask for the entities with each preference and check the answers and refusals; then, with a stand-in cloud, check that the same rules hold with the cloud on.

### Tests for User Story 2 (write first, they must fail) ⚠️

- [X] T029 [P] [US2] Extend `tests/Scriptorium.Core.Tests/Services/DocumentEntityServiceTests.cs` with the routing cases, the same as `DocumentSummaryServiceTests` has for summaries: with the cloud off, Auto and Local are answered locally; Cloud gives `ModelUnavailable` with `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` and no model is called; a private document with Cloud gives `PreferenceNotAllowed` with `This document is private, so it can only be answered on this computer. Choose Auto or Local.` and no model is called; the checks of the document and of the text come **before** routing (an unknown document, a document not ready and a text under 50 characters keep their kinds with the preference Cloud). With a router built for the test with `CloudEnabled` true over a configured stand-in `ICloudAIProvider`: with the default threshold (2) a long document under Auto stays local; with `ComplexityThreshold` 0 a long document (80 characters) under Auto is answered by the stand-in with `Backend` Cloud and a short one (60 characters) stays local; a private document under Auto or Local never reaches the stand-in; preference Cloud on a non-private document reaches it **and its reply is read and checked like a local one**; a stand-in that answers `Unavailable` or `Timeout` gives `ModelUnavailable` or `Timeout` with its text and the local provider is **never called**; and a **theory over the whole table** (SC-004): private yes/no × preference Auto/Local/Cloud × cloud state off / on and configured / on but not configured × short/long text, against an independent oracle, giving the same backend or failure kind that `ModelRouterTests` gives for a question
- [X] T030 [P] [US2] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/EntityEndpointTests.cs` with the cases of contracts/entities-api.md: `preference` absent, `null`, empty, `"auto"` and `"Local"` are answered by the mocked local provider; `"cloud"` with the delivered settings gives `503` with the not-available message and the local provider is **never** called; `"cloud"` on a private document gives `409` with the private message and no provider is called; `"turbo"` gives `400` with `The preference must be auto, local or cloud.` and is checked **before** an unknown id would give `404`; `"cloud"` on an unknown document, on a document not ready and on a tiny text keep their `404`, `409`, `409`. Cloud on by configuration (`Routing:CloudEnabled` true) with a stand-in `ICloudAIProvider` replacing `ClaudeProvider`: `"cloud"` gives `200` with `"backend":"cloud"` and the entities of the stand-in's reply that are in the document; the same on a private document gives `409`; Auto and Local on a private document give `"local"`; a stand-in that answers `Unavailable` gives `503` and `Timeout` gives `504` with its own text, and the local provider is never called; with cloud on but the **real** `ClaudeProvider` and the key option set to empty (`UseSetting("Anthropic:ApiKey", "")`), `"cloud"` gives `503` with the not-available message and Auto is answered locally. No test registers a key or reaches the network
- [X] T031 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts`: `extractEntities('abc', 'cloud')` sends `{ preference: 'cloud' }`
- [X] T032 [P] [US2] Extend the entities `describe` block of `ask-panel.component.spec.ts`: **Extract entities** sends the preference chosen in the **Answer with** selector (`auto` by default, `local`, `cloud`); the Cloud choice is not disabled; a `503` and a `409` response show the server's message as it is and leave no card; the selector goes back to Auto when another document is opened, when the panel is closed and on **New conversation**, whether or not entities were asked for. Existing specs stay as they are

### Implementation for User Story 2

- [X] T033 [US2] In `src/Scriptorium.API/Endpoints/EntityEndpoints.cs` read `body?.Preference` with `ModelPreferenceParser.TryParse` **before anything else** (an invalid value is `400` with `The preference must be auto, local or cloud.`) and pass the result to `ExtractAsync` instead of the fixed `ModelPreference.Auto`. `DocumentEntityService` already routes with the preference it is given: no change expected there. Every method under 30 lines
- [X] T034 [US2] Run `dotnet build` and `dotnet test`: T029 and T030 pass and everything else is still green. If a router or service test fails, it shows a gap: fix the production code (never weaken the test) and tell the user what was found
- [X] T035 [US2] Check `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts`: **Extract entities** already passes `this.preference()` since T026, so no code change is expected; if T032 shows otherwise, fix the component (it sends the preference and shows `backend`; it decides nothing)
- [X] T036 [US2] Run in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T031 and T032 pass, everything else is still green and no new warning

### Manual check for User Story 2

- [X] T037 [US2] With the user: run quickstart scenario 3 (Cloud on the normal and on the private document, and an invalid preference, by `curl`; then the same on the page with the selector) and scenario 8 (restart the throwaway API with `Routing__CloudEnabled=true` and an **empty key**: Cloud still gives the not-available message and Auto answers locally). Check `isPrivate` of the private file in the list first. Never use a real key. **Wait for the user's OK before anything is committed**; record the outcome in `specs/007-entity-extraction/validation-results.md`

**Checkpoint**: An extraction obeys the same preference and privacy rules as a question and a summary, and the cloud is still off and unused

---

## Phase 5: User Story 3 - A long document says what the extraction covers (Priority: P3)

**Goal**: The entities of a document longer than the limit the model reads say that they are based only on the first part; a shorter one shows no notice; and an entity that is only in the part not read is never shown.

**Independent Test**: Extract the entities of a document longer than 8,000 characters and of one shorter, and check that only the first shows the notice.

### Tests for User Story 3 (write first, they must fail) ⚠️

- [X] T038 [P] [US3] Extend `tests/Scriptorium.Core.Tests/Services/DocumentEntityServiceTests.cs`: a text longer than the limit gives a success with `Truncated` true and the model receives only the first `MaxDocumentCharacters` characters of it (the end of the text is not in the request); a text at or under the limit gives `Truncated` false; the cut never ends on half of a character; **an entity that the model names and that is in the document only after the cut is dropped**, even though it is in the full text (the check is against the text that was read)
- [X] T039 [P] [US3] Extend `tests/Scriptorium.Infrastructure.Tests/Endpoints/EntityEndpointTests.cs`: a document of 9,000 characters (default limit 8,000) gives `"truncated": true`, the request the mock receives does not contain the last characters of the text, and an entity that the mock names and that appears only in those last characters is absent from the response; a short document gives `"truncated": false`
- [X] T040 [P] [US3] Extend the entities `describe` block of `ask-panel.component.spec.ts`: when `truncated` is true the card shows a note with `These entities are based only on the first part of the document, which is too long to be read in full.`; when false there is no note; the note is not shown for a failed request and goes away with the card

### Implementation for User Story 3

- [X] T041 [US3] In `ask-panel.component.html` show the note of T040 in the entities card when the response says `truncated`, with the same `notice` style the answers and the summary use. The cut itself already exists (`EntityPromptBuilder` and the DTO carry `Truncated`, and the verifier checks against `TextRead`, since User Story 1)
- [X] T042 [US3] Run `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T038 to T040 pass and everything else is still green. If T038 or T039 fail, the cut or the check is wrong: fix the production code and tell the user

### Manual check for User Story 3

- [X] T043 [US3] With the user: run quickstart scenario 5 (`long.txt` gives `"truncated": true`, `facts.txt` does not) and the notice on the page (scenario 6, the notice part). **Wait for the user's OK before anything is committed**; record the outcome in `specs/007-entity-extraction/validation-results.md`

**Checkpoint**: All three stories work; the extraction never pretends to cover a text it did not read and never shows an entity that is not in it

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, review against the constitution, the final regression run, and the pull request

- [X] T044 [P] Update `README.md`: the roadmap row "Entity extraction" becomes done; a line in "What it does today" about **Extract entities** (people, organizations, dates, amounts and places, grouped, each once and as written, only what is really in the text, from the first part of the text, produced each time, nothing stored, same model choice and label as the questions); the API table gets `POST /api/documents/{id}/entities` (optional `preference`; `200` with `groups`, `truncated` and `backend`; `400`, `404`, `409` for not ready, too little text or Cloud on a private document, `503`, `504`, `500`); link the feature's contract and spec folder
- [X] T045 [P] Update `docs/architecture.md`: a new flow **4.5 Extracting entities** (a sequence diagram: page, endpoint, `DocumentEntityService`, `RoutedDocument`, `EntityPromptBuilder`, provider, `EntityReplyParser`, `EntityVerifier`; the refusals; the dropped entities; nothing stored); `RoutedDocument` and the entity classes in the layers table, and the summary flow (4.4) saying that its checks, scoring, routing and provider call are now in `RoutedDocument`; section 7 says that an extraction is scored like a summary (empty question, score 0 or 1, so local with the default threshold); the web table mentions the **Extract entities** button
- [X] T046 Review the new and changed code against the constitution: every class has one responsibility and every method is under 30 lines (`DocumentEntityService.ExtractAsync`, `RoutedDocument` and `EntityVerifier` in particular); no `!` without a comment, no `.Result`/`.Wait()`, no `dynamic` or reflection; **`ModelRouter` is still the only place that chooses between local and cloud** (`git grep` for other decisions on `ModelBackend` or `CloudEnabled` outside the router and the wiring); no `HttpClient` outside `OllamaProvider` and `ClaudeProvider`; no Ollama and no cloud code in the new classes; `DocumentSummaryService` is smaller than before and its public shape is unchanged. Run `git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'`: only code that reads the key option, no key value anywhere. Confirm with `git diff main --name-only` that **no `.csproj`, `package.json` or `package-lock.json` changed**, that no migration or entity was added and that `AiRequest`, `IAIProvider` and both providers are untouched. List any deviation and ask the user before fixing anything beyond the feature
- [X] T047 Run `dotnet build`, the full `dotnet test`, and in `src/Scriptorium.Web` the full `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`; compare with the T001 baseline (no test removed, only additions, no new warnings; the only change to an existing spec is the list of methods of the API spy); run quickstart scenarios 9 and 10; complete `specs/007-entity-extraction/validation-results.md` with the results of scenarios 1 to 10, saying plainly that the real cloud service was never called and that the quality of the entities was judged by reading them
- [X] T048 Hand over to the user: summarise the work and the validation. **Only with the user's OK**: commit what is not yet committed (messages in English, descriptive, with the required `Co-Authored-By` line), push `009-entity-extraction`, and open **one** pull request to `main` for the whole feature; merge it only when the user says so, then update `main` and delete the branch locally and on the remote. Never commit on `main`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **Foundational (Phase 2)**: after Setup; blocks all three stories
- **User Story 1 (Phase 3)**: after Foundational
- **User Story 2 (Phase 4)**: after User Story 1, because it changes the same endpoint file and the same panel files and replaces the fixed `Auto` of T023
- **User Story 3 (Phase 5)**: after User Story 2 (same test files and the same card); it adds mostly tests and one note in the page
- **Polish (Phase 6)**: after all three stories

### Within Each Story

- Foundational: T003 first; T004 before T005; T006 closes the phase
- Story 1: T007 to T014 first; T015 first among the code; T016 before T017 to T020; T020 before T021; T004, T017, T018, T019 and T020 before T022; T021 and T022 before T023; T024 closes the backend; T025 before T026; T027 closes the frontend, then the manual check
- Story 2: T029 to T032 first; T033, T034; T035 is a check, after T033; T036; the manual check last
- Story 3: T038 to T040 first, T041, T042, then the manual check
- The manual checks (T028, T037, T043) come before any commit of the story

### Parallel Opportunities

- T002 can be done at any time in Phase 1
- T007 to T014 are in eight different files (T013 and T014 extend two existing specs) and can be written together
- T015 is a new file with no dependency and can be written at any time in the story
- T029 to T032 extend four different files; T038 to T040 extend three different files
- T044 and T045 are in different files
- **Not** parallel: T022, T023, T026 (they change production files that other tasks read); T033 and T041 (same endpoint and component files as the tasks before them); T011, T029, T038 (same test file in different phases) and T012, T030, T039 (same); T014, T032, T040 (same spec file)

### Parallel Example: tests of User Story 1

```text
# Write the eight test files together before any production code of the story:
Task: "T007 EntityPromptBuilderTests in tests/Scriptorium.Core.Tests/Services/EntityPromptBuilderTests.cs"
Task: "T008 EntityReplyParserTests in tests/Scriptorium.Core.Tests/Services/EntityReplyParserTests.cs"
Task: "T009 EntityVerifierTests in tests/Scriptorium.Core.Tests/Services/EntityVerifierTests.cs"
Task: "T010 EntityResultTests and EntityDtosTests in tests/Scriptorium.Core.Tests/"
Task: "T011 DocumentEntityServiceTests in tests/Scriptorium.Core.Tests/Services/DocumentEntityServiceTests.cs"
Task: "T012 EntityEndpointTests in tests/Scriptorium.Infrastructure.Tests/Endpoints/EntityEndpointTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 and T002: baseline and fake files
2. Foundational: T003 to T006 (the move into `RoutedDocument`, with no change to any existing test)
3. T007 to T027: the entities of a ready document, end to end, on the local model
4. T028: try it with the user
5. **Stop and validate**: the user tries scenarios 1, 2, 4, 6 and 7 and gives the OK; only then commit (on `009-entity-extraction`). Do not open the pull request yet: the preference and the long-document notice come with User Stories 2 and 3

### Incremental Delivery

1. Foundation + User Story 1 → tried by the user → commit
2. User Story 2 (the preference, the refusals, the stand-in cloud proof) → tried by the user → commit
3. User Story 3 (the notice for a long document, and the check against the text read) → tried by the user → commit
4. Polish: README, architecture document, review, final regression run → commit → one pull request for the whole feature

---

## Notes

- [P] tasks = different files, no dependencies
- The decision lives only in `ModelRouter`; the page sends the preference and shows `backend`
- `RoutedDocument` is **moved** out of `DocumentSummaryService`, not rewritten: the existing summary tests are the proof, and they are not edited
- The model answers one entity per line (`kind: text`); `EntityReplyParser` and `EntityVerifier` are pure functions with exact tests; **an entity that is not in the text read is never shown** (this is the rule that matters most with a small model)
- A reformatted but honest entity (a date the model rewrites) is dropped: this is the price of never showing an invented one (plan, point 3)
- Auto scores an extraction like a summary: the score is 0 or 1, so with the default threshold it stays local under Auto
- Nothing is stored: no entity, no column, no migration, no cache; no change to `AiRequest` or to the providers
- The cloud stays **off** in every manual step; no key is set, no key is asked for, and the real cloud service is never called
- Fake files only: no document uploaded by the user is read or used for tests or diagnosis; check `isPrivate` after uploading
- If a task shows that the framework behaves differently from research.md, stop and update research.md before going on, and tell the user
