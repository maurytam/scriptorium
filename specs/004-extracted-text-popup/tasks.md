---

description: "Task list for Extracted-Text Popup"

---

# Tasks: Extracted-Text Popup

**Input**: Design documents from `/specs/004-extracted-text-popup/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/text-preview-api.md, contracts/text-popup-ui.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity). Backend: xUnit + FluentAssertions + Moq, plus endpoint tests on the in-process `ApiTestHost`. Frontend: Jasmine/Karma specs next to each component and service, as the question panel has. Tests are written before the code they cover and must fail first. All test documents are fake files; no document uploaded by the user is read or used.

**Organization**: Tasks are grouped by user story (from spec.md). The server-side cut and the preview endpoint are shared by both stories, so they sit in Foundational: the tests of the cut itself (T002, T004, T006) are the ones that prove User Story 2 on the server side. User Story 1 is the popup that shows the text; User Story 2 adds the "text is cut" notice to the popup. No new NuGet or npm package, no migration.

**Commits**: Nothing is committed until the user has tried the story in the browser and given the OK (project rule). Work stays on branch `005-extracted-text-popup`. Because the endpoint always cuts at the limit, User Story 1 alone would show a cut text without saying so: User Story 1 and User Story 2 are therefore tried and committed one after the other but merged together in the same pull request.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/src/app/documents/`, `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/Endpoints/`. Manual-test files go in `~/Desktop/prove-scriptorium`, produced by a throwaway generator kept outside the repository (fake content only).

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" can be checked at the end

- [X] T001 Run `dotnet build` and `dotnet test` on branch `005-extracted-text-popup` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`, before any change; note the build warnings and the number of passing tests per test project (backend: Core and Infrastructure; frontend: Karma). If the frontend tests cannot run (no Chrome, missing `node_modules`), say so to the user before going on (reference for T013, T018 and T032)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The server side shared by both stories: the cut shared with the question flow, the preview use case and its endpoint, and the browser plumbing (model, API call, selection service)

**⚠️ CRITICAL**: No story work can start until this is complete

### Tests for the server side (write first, they must fail) ⚠️

- [X] T002 [P] Create `tests/Scriptorium.Core.Tests/Services/TextExcerptTests.cs` for `TextExcerpt.Of(text, maxCharacters)`: a text shorter than the limit is returned unchanged, not truncated, with its length as original length; a text exactly at the limit is not truncated; a longer text is cut to exactly the first `maxCharacters` characters, truncated, with the original length; a cut that would fall between the two halves of a surrogate pair (use an emoji) moves one unit back so no character is split; a limit of 0 gives an empty truncated text; an empty text is not truncated
- [X] T003 [P] Create `tests/Scriptorium.Core.Tests/Results/TextPreviewResultTests.cs` in the style of `AskResultTests`: a success carries content, truncated and total characters and `IsSuccess` is true with no failure kind or error; a failure carries its kind (`NotFound`, `NotReady`) and error and `IsSuccess` is false
- [X] T004 [P] Create `tests/Scriptorium.Core.Tests/Services/DocumentTextPreviewServiceTests.cs` with a `Mock<IDocumentRepository>` and `QaOptions`: an unknown id gives `NotFound`; a processing and a failed document give `NotReady` with the message `Document is not ready. Current status: processing.` (status in lowercase); a ready document with a text within the limit gives that text, not truncated, with its length; a ready text longer than `MaxDocumentCharacters` (use a small limit such as 10) gives the first characters, truncated, with the whole length; a ready document whose text is missing in the repository gives `NotReady` with the message `The extracted text of this document is not available.` (not the "Current status" wording, which would read "ready"); the repository is asked for the text only when the document is ready
- [X] T005 [P] Extend `tests/Scriptorium.Core.Tests/Dtos/DocumentDtosTests.cs` with a test for `ExtractedTextPreviewDto.From(...)`: the document id, content, `Truncated` and `TotalCharacters` of a successful result are copied unchanged
- [X] T006 [P] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentTextPreviewEndpointTests.cs` on `ApiTestHost` (same style as `DocumentUploadEndpointTests`): `GET /api/documents/{id}/text/preview` returns `404` for an unknown id; `409` with `Document is not ready. Current status: failed.` for a failed document (an encrypted or empty file); `200` with the whole text, `truncated` false and `totalCharacters` equal to its length for a short `.txt`; for a `.txt` of 9,000 characters (default limit 8,000) `200` with `truncated` true, `totalCharacters` 9,000 and a `content` that is exactly the first 8,000 characters of what `GET /api/documents/{id}/text` returns for the same document; the existing `GET /text` still returns the whole text of that long document (regression)

### Implementation of the server side

- [X] T007 Create `TextExcerpt` in `src/Scriptorium.Core/Services/TextExcerpt.cs`: a small immutable type (record) with `Text`, `Truncated` and `OriginalLength`, and a static `Of(string text, int maxCharacters)` that applies the rule of data-model.md (within the limit unchanged; otherwise the first characters; a high surrogate at the cut point moves the cut one unit back). Move the logic of `QuestionPromptBuilder.FitToBudget` here, unchanged in behaviour. Methods under 30 lines
- [X] T008 Change `QuestionPromptBuilder` in `src/Scriptorium.Core/Services/QuestionPromptBuilder.cs`: replace its private `FitToBudget` with `TextExcerpt.Of(documentText, _options.MaxDocumentCharacters)`. Behaviour must not change: `QuestionPromptBuilderTests` and `DocumentQuestionServiceTests` stay as they are and stay green
- [X] T009 Create the failure kinds and the result: `src/Scriptorium.Core/Enums/TextPreviewFailureKind.cs` (`NotFound`, `NotReady`) and `src/Scriptorium.Core/Results/TextPreviewResult.cs`, shaped like `AskResult` (private constructor, `Success(content, truncated, totalCharacters)`, `Failure(kind, error)`, `IsSuccess` with `MemberNotNullWhen`, no `!`)
- [X] T010 Create `DocumentTextPreviewService` in `src/Scriptorium.Core/Services/DocumentTextPreviewService.cs`, a `public sealed class` receiving `IDocumentRepository` and `QaOptions` by constructor, with `Task<TextPreviewResult> GetPreviewAsync(Guid documentId, CancellationToken ct)`: unknown document gives `NotFound`; a document that is not ready gives `NotReady` with `Document is not ready. Current status: <status in lowercase>.` (the wording `GetTextAsync` uses today); a ready document whose text is missing gives `NotReady` with `The extracted text of this document is not available.`; otherwise `TextExcerpt.Of(content, options.MaxDocumentCharacters)` as a success. Methods under 30 lines
- [X] T011 Add `ExtractedTextPreviewDto(Guid DocumentId, string Content, bool Truncated, int TotalCharacters)` with a static `From(Guid documentId, TextPreviewResult result)` to `src/Scriptorium.Core/Dtos/DocumentDtos.cs`
- [X] T012 Map the endpoint in `src/Scriptorium.API/Endpoints/DocumentEndpoints.cs`: `group.MapGet("/{id:guid}/text/preview", GetTextPreviewAsync)`, a thin method that calls `DocumentTextPreviewService` and maps the result: success to `200` with the DTO, `NotFound` to `404`, `NotReady` to `409` with `ErrorDto`; and register `DocumentTextPreviewService` as a singleton in `src/Scriptorium.API/Program.cs` next to the other services (check how `DocumentQuestionService` is registered and do the same). `GET /text` stays untouched
- [X] T013 Run `dotnet build` and `dotnet test`: T002 to T006 now pass, every earlier test is green, no new build warnings compared with T001, and `git diff` shows no change to any `.csproj` file

### Browser plumbing (write the specs first) ⚠️

- [X] T014 [P] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts` with a test for `getTextPreview(id)`: it sends `GET /api/documents/{id}/text/preview` and emits the `TextPreview` body (`documentId`, `content`, `truncated`, `totalCharacters`)
- [X] T015 [P] Create `src/Scriptorium.Web/src/app/documents/text-popup/text-popup-selection.service.spec.ts` in the style of `conversation-selection.service.spec.ts`: starts with no document; `open` selects a document; `close` clears it; opening another document replaces the first
- [X] T016 Add the `TextPreview` interface to `src/Scriptorium.Web/src/app/documents/document.models.ts` (`documentId`, `content`, `truncated`, `totalCharacters`) and `getTextPreview(id: string): Observable<TextPreview>` to `src/Scriptorium.Web/src/app/documents/document-api.service.ts`
- [X] T017 Create `TextPopupSelectionService` in `src/Scriptorium.Web/src/app/documents/text-popup/text-popup-selection.service.ts`: `providedIn: 'root'`, a signal with the open `DocumentSummary` (`document` read-only, `open(document)`, `close()`), the same pattern as `ConversationSelectionService`; it must not use or touch that service (FR-010)
- [X] T018 Run, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`: T014 and T015 now pass and every earlier spec is green

**Checkpoint**: The server returns the cut text with its counts, the question flow is unchanged, and the browser can ask for a preview and remember which document is open

---

## Phase 3: User Story 1 - Read the extracted text of a document (Priority: P1) 🎯 MVP

**Goal**: A Text button on every ready row opens a modal popup, read-only, with the document's name and its extracted text as plain text (line breaks kept); it closes with the Close button, Escape or a click outside and gives the focus back to the button (FR-001 to FR-005, FR-009, FR-010, FR-011).

**Independent Test**: Upload a short text file and a signed PDF, wait until both are ready, open the popup of each and read the text (for the PDF, starting with the signature note); close it in the three ways and check the list is unchanged. Processing and failed rows have no button.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [X] T019 [P] [US1] Create `src/Scriptorium.Web/src/app/documents/text-popup/text-popup.component.spec.ts` (Jasmine, a spy on `DocumentApiService.getTextPreview`, the real `TextPopupSelectionService`): with no document selected the dialog is not open; selecting a document opens it with the title `Extracted text of <file name>` and `Loading…` while the request is pending; a loaded preview shows the content with its line breaks preserved and no cutting notice; text such as `<b>bold</b> <script>alert(1)</script>` appears literally (the element's text contains it, and no `b` or `script` element exists inside the text area); there is no editable control (`textarea`, `input`, `contenteditable`); a failed request (`404`, `409` with `{ error }`, and a network error) shows `The text is not available.` or the server's message, and the Close button still works; the Close button clears the selection; the dialog's `cancel` event (Escape) clears the selection; a click on the backdrop (the dialog element itself) clears it and a click inside the content does not; selecting document B while A is still loading shows only B's text (the earlier request is cancelled); opening the popup does not call `ConversationSelectionService`; focus returns to the button that opened the popup (focus a button in the test page, open the popup, close it, and `document.activeElement` is that button; the Karma browser is a real Chrome, so the native `<dialog>` behaviour is exercised)
- [X] T020 [P] [US1] Extend `src/Scriptorium.Web/src/app/documents/document-list/document-list.component.spec.ts`: a ready row shows a `Text` button with the accessible name `View extracted text of <file name>`; processing and failed rows do not; clicking it calls `TextPopupSelectionService.open` with that row's document and does not call `ConversationSelectionService.open`; the existing Ask and Delete behaviour is unchanged

### Implementation for User Story 1

- [X] T021 [US1] Create the popup component in `src/Scriptorium.Web/src/app/documents/text-popup/text-popup.component.ts` and `.html` (standalone, selector `app-text-popup`): a native `<dialog>` opened with `showModal()` when a document is selected and closed with `close()` when it is cleared (an `effect` on the selection signal, with a view child for the dialog); it loads the preview with `switchMap` on the selected document and keeps three signals (loading, preview, error); the title, `Loading…`, the text in a `pre`-style element bound by interpolation (never `innerHTML`) with `white-space: pre-wrap` and its own scrolling, a `Close` button, `(cancel)` and a backdrop click both clearing the selection; the failure message as in T019. No cut and no counting in the component (it only renders what the server sent). Methods under 30 lines
- [X] T022 [US1] Style the popup in `src/Scriptorium.Web/src/styles.css` (the other components have no stylesheet of their own): readable width and height inside the window, text area scrolling, the app's light/dark theme variables, `::backdrop`, and `body:has(dialog[open]) { overflow: hidden; }` so the page behind does not scroll
- [X] T023 [US1] Add the **Text** button to `src/Scriptorium.Web/src/app/documents/document-list/document-list.component.html` (inside the existing `document.status === 'ready'` block, before **Ask**, same `secondary` styling, `[attr.aria-label]="'View extracted text of ' + document.fileName"`) and `viewText(document)` to `document-list.component.ts`, which calls `TextPopupSelectionService.open(document)` (inject the service like the others)
- [X] T024 [US1] Add `<app-text-popup />` to `src/Scriptorium.Web/src/app/app.component.html` after `<app-ask-panel />` and its import to `app.component.ts`
- [X] T025 [US1] Run, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T019 and T020 now pass, every earlier spec is green, the build has no new warnings

### Manual check for User Story 1

- [X] T026 [US1] Prepare fake files in `~/Desktop/prove-scriptorium` with a throwaway generator outside the repository (or by hand): `text-short.txt`, `text-markup.txt` (containing `<b>bold</b> <script>alert(1)</script>` and a few lines), each with different content; the signed PDFs of feature 003 can be reused. With the user: run quickstart scenarios 1, 2 (including the keyboard-only pass), 3, 4, 5 and 9 against the API on a throwaway database and port (the user's own API on 5034 must be restarted by the user with the new code if it is used), and wait for the user's OK before anything is committed; record the outcome in `specs/004-extracted-text-popup/validation-results.md`

**Checkpoint**: The user can read the text of any ready document in a popup, and nothing else in the app changed

---

## Phase 4: User Story 2 - Long texts are shown cut, and the popup says so (Priority: P2)

**Goal**: When the server says the text was cut, the popup shows the notice `Showing the first <shown> of <total> characters.` (FR-006); when it was not cut, nothing is said (FR-007). The cut itself and its limit are already in place from Phase 2 (T002, T004, T006, T007 to T012).

**Independent Test**: Upload a text longer than 8,000 characters and one shorter; the long one shows only its beginning with the notice and the real counts; the short one shows in full with no notice.

### Tests for User Story 2 (write first, they must fail) ⚠️

- [X] T027 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/text-popup/text-popup.component.spec.ts`: a preview with `truncated: true`, `content` of 7,999 characters and `totalCharacters: 52310` shows the note `Showing the first 7,999 of 52,310 characters.` (shown number = `content.length`, thousands separators) above the text; a preview with `truncated: false` shows no such note; the note disappears when another, uncut document is opened; the note has `role="note"`

### Implementation for User Story 2

- [X] T028 [US2] Add the notice to `src/Scriptorium.Web/src/app/documents/text-popup/text-popup.component.html`: when `preview().truncated`, a `<p role="note">` above the text reading `Showing the first {{ preview().content.length | number }} of {{ preview().totalCharacters | number }} characters.` (import `DecimalPipe` in the component); style it in `src/Scriptorium.Web/src/styles.css` like the existing `notice` of the question panel. No computing beyond `content.length`
- [X] T029 [US2] Run, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T027 passes and everything else is still green

### Manual check for User Story 2

- [x] T030 [US2] Add to `~/Desktop/prove-scriptorium` a `text-long.txt` of more than 8,000 characters (fake, different from the others). With the user: run quickstart scenarios 6 (including the long-lines and page-scroll checks), 7 (needs Ollama and `qwen3.5:4b`), 8 and 11, and wait for the user's OK before anything is committed; record the outcome in `specs/004-extracted-text-popup/validation-results.md`

**Checkpoint**: Both stories work; a cut text always says so and a whole text never does

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, review against the constitution and the final regression run

- [ ] T031 [P] Add one line to the "What it does today" list in `README.md` saying that a ready document has a **Text** button that opens a read-only popup with the extracted text, cut at the same limit the model reads and saying so when it is cut
- [ ] T032 Review the new and changed code against the constitution: `TextExcerpt`, `DocumentTextPreviewService`, the endpoint method and the popup component each have one responsibility; every method is under 30 lines; no `!` without a comment, no `.Result`/`.Wait()`, no reflection; no `innerHTML` anywhere in the popup (`grep`); no business logic in the components (no cutting or counting beyond `content.length`); `QuestionPromptBuilder` and the preview service share `TextExcerpt` and no copy of the cut remains; confirm with `git diff` that no `.csproj`, `package.json` or `package-lock.json` changed (no new package); list any deviation and ask the user before fixing anything beyond the feature
- [ ] T033 Run `dotnet build`, the full `dotnet test`, and in `src/Scriptorium.Web` the full `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`; compare with the T001 baseline (no test removed, no new warnings); run quickstart scenario 10; and complete `specs/004-extracted-text-popup/validation-results.md` with the results of scenarios 1 to 11

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **Foundational (Phase 2)**: after Setup; blocks both stories
- **User Story 1 (Phase 3)**: after Foundational
- **User Story 2 (Phase 4)**: after User Story 1, because it changes the same component, template and spec files; it is independent in behaviour but not in files
- **Polish (Phase 5)**: after both stories

### Within Each Story

- Server side: tests (T002 to T006) first, then confirm they fail, then T007 (the cut) before T008 (the prompt builder uses it) and before T010 (the service uses it); T009 (result and failure kinds) before T010; T010 and T011 before T012 (the endpoint maps them); T013 closes the server side
- Browser plumbing: T014 and T015 first; T016 before T017 only by convenience (T017 uses `DocumentSummary`, which exists); T018 closes it
- Story 1: T019 and T020 first; T021 (component) before T024 (shell uses it); T022 (styles) and T023 (button) can go in any order after T017; T025 closes the code, then the manual check
- Story 2: T027 first, then T028, then T029
- The manual check (T026, T030) before any commit of the story

### Parallel Opportunities

- T002, T003, T004, T005 and T006 are in different files and can be written together
- T014 and T015 are in different files and can be written together
- T019 and T020 are in different files and can be written together
- T031 (README) can be done at any time after T012
- T007, T008, T010 and T012 touch ordered files and are **not** parallel; T021, T028 and T019/T027 touch the same component files and are **not** parallel

### Parallel Example: Server-side tests

```text
# Write all five test files together before any production code:
Task: "T002 TextExcerptTests in tests/Scriptorium.Core.Tests/Services/TextExcerptTests.cs"
Task: "T003 TextPreviewResultTests in tests/Scriptorium.Core.Tests/Results/TextPreviewResultTests.cs"
Task: "T004 DocumentTextPreviewServiceTests in tests/Scriptorium.Core.Tests/Services/DocumentTextPreviewServiceTests.cs"
Task: "T005 DocumentDtosTests extension in tests/Scriptorium.Core.Tests/Dtos/DocumentDtosTests.cs"
Task: "T006 DocumentTextPreviewEndpointTests in tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentTextPreviewEndpointTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 baseline
2. Foundational: server side T002 to T013, browser plumbing T014 to T018
3. T019 to T025: the Text button and the popup
4. T026: try it in the browser
5. **Stop and validate**: the user tries it and gives the OK; only then commit (on `005-extracted-text-popup`). Do not open the pull request yet: until User Story 2 is in, a long text would be shown cut without saying so

### Incremental Delivery

1. Foundation + User Story 1 → tried by the user → commit
2. User Story 2 (the cut notice) → tried by the user → commit
3. Polish: README, review, final regression run → commit → one pull request for the whole feature

---

## Notes

- [P] tasks = different files, no dependencies
- The cut happens only in the server; the component renders what it receives and shows `content.length` as the number of characters shown
- The existing `GET /api/documents/{id}/text` must stay as it is: it is still the way to read a whole text
- Fake files only: no document uploaded by the user is read or used for tests or diagnosis
- If a task shows that the browser or the framework behaves differently from research.md (for instance `<dialog>` focus return or `showModal` in the test browser), stop and update research.md before going on, and tell the user
