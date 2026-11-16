# Implementation Plan: Extracted-Text Popup

**Branch**: `005-extracted-text-popup` | **Date**: 2026-11-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-extracted-text-popup/spec.md`

## Summary

The user cannot see the text Scriptorium extracts from a document. This feature adds a **Text** button to the row of
every ready document in the list; it opens a modal popup, read-only, with the document's name and its extracted text. A
text longer than the limit the model reads (`Qa:MaxDocumentCharacters`, 8,000 by default, FR-008) is cut at the end, and the
popup says how many characters are shown out of the total.

The cut is made **on the server**, so a 50 MB document never travels whole to the browser to show 8,000 characters, and so the
popup stops exactly where the model's reading stops. A new endpoint `GET /api/documents/{id}/text/preview` returns the cut
text with `truncated` and `totalCharacters`; the existing `GET /api/documents/{id}/text` is not touched. The surrogate-safe
cut that `QuestionPromptBuilder` already does is moved into one small shared class used by both the prompt builder and the new
preview service, so the two can never disagree. In the browser a small selection service (the same pattern as the question
panel's) says which document is open, and a dialog component built on the native `<dialog>` element shows it: Escape, focus
trap, inert background and returning the focus to the Text button come from the browser, with no new package.

Nothing is stored, no migration, no new NuGet or npm package.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 5.6 / Angular 19 (frontend, standalone components and signals)

**Primary Dependencies**: None new. Backend: existing ASP.NET Core minimal API, `Scriptorium.Core`. Frontend: existing Angular
`HttpClient`, RxJS. The popup uses the browser's native `<dialog>` element, not a UI library.

**Storage**: None added. The preview is computed from the stored `ExtractedText` at request time; no schema change.

**Testing**: Backend: xUnit + FluentAssertions + Moq (Core tests for the cut, the service and the DTO; endpoint tests in the
existing `Scriptorium.Infrastructure.Tests/Endpoints` style). Frontend: Jasmine/Karma specs next to each component and service,
as in the question panel. The real browser behaviour (Escape, click outside, focus return, scroll lock) is checked by hand with
[quickstart.md](./quickstart.md).

**Target Platform**: Local macOS web app (unchanged); a current desktop browser

**Project Type**: Web application; this feature touches the Core (one use case), the API (one endpoint) and the Angular UI

**Performance Goals**: The popup shows the text within 2 seconds for a text within the limit (SC-001). The preview response
is at most the limit plus a few fields (about 8 KB by default), whatever the size of the document.

**Constraints**: No new NuGet or npm package; read-only, nothing stored or changed (FR-003, FR-010); text always rendered as
plain text, never as markup (FR-005); the limit must be the one used for questions (FR-008); the existing `/text` endpoint and
the question flow are unchanged; no business logic in the components (the cut is not done in the browser)

**Scale/Scope**: One new Core class for the cut, one use-case service with its result type, one DTO, one endpoint (about 40 lines
of backend production code); one service, one component, one button and a few model/API lines in the frontend (about 150 lines
of TypeScript, template and styles)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Clean Architecture | The use case and the cut live in `Scriptorium.Core` and reach data only through `IDocumentRepository`; the endpoint in the API only maps the result to HTTP; Core does not reference Infrastructure | PASS |
| II. Async-First | The service awaits repository calls; no `.Result`/`.Wait()` | PASS |
| III. Nullable Reference Safety | Result type models success/failure with nullable members and `MemberNotNullWhen`, like `AskResult`; no `!` | PASS |
| IV. Result<T> | The service returns a result object with a failure kind (not found, not ready); no exception crosses a layer | PASS |
| V. Constructor Injection | `DocumentTextPreviewService` receives the repository and `QaOptions` by constructor; registered in `Program.cs`. Frontend services use `inject()`, like the existing ones | PASS |
| VI. Single Responsibility & Simplicity | Cutting a text, reading a preview and mapping to HTTP are three separate units; methods stay under 30 lines. The cut is shared with `QuestionPromptBuilder` instead of copied | PASS |
| VII. Provider Isolation | No AI code involved | PASS |
| VIII. Test Parity | New test classes for `TextExcerpt`, `DocumentTextPreviewService`, `TextPreviewResult`, the DTO and the endpoint; specs for the new frontend service and component | PASS |
| Workflow: interface before implementation | The Core use case follows the existing convention (`DocumentQuestionService`, `DocumentDeletionService` are concrete classes over repository interfaces); no new infrastructure dependency is introduced, so no new interface is needed | PASS |
| Non-negotiable: no new package | Native `<dialog>`, no Angular CDK, no NuGet | PASS |
| Non-negotiable: no business logic in view components | The component only renders the DTO; the cut and the counts come from the server | PASS |

**Post-design re-check (after Phase 1)**: unchanged, all PASS. No entry in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-extracted-text-popup/
├── plan.md                      # This file
├── research.md                  # Phase 0: where to cut, endpoint shape, popup mechanism, decisions
├── data-model.md                # Phase 1: the preview, the result type, the browser state
├── quickstart.md                # Phase 1: manual validation scenarios
├── contracts/
│   ├── text-preview-api.md      # Phase 1: GET /api/documents/{id}/text/preview
│   └── text-popup-ui.md         # Phase 1: the button, the popup, its states and wording
├── checklists/
│   └── requirements.md          # From /speckit-specify
└── tasks.md                     # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Services/
│   │   ├── TextExcerpt.cs                   # NEW: cuts a text at a limit, never inside a character; used by both below
│   │   ├── DocumentTextPreviewService.cs    # NEW: the use case "preview the extracted text of a document"
│   │   └── QuestionPromptBuilder.cs         # CHANGED: its private cut is replaced by TextExcerpt (same behaviour)
│   ├── Results/
│   │   └── TextPreviewResult.cs             # NEW: success (content, truncated, total) or failure (not found / not ready)
│   ├── Enums/
│   │   └── TextPreviewFailureKind.cs        # NEW: NotFound, NotReady
│   └── Dtos/
│       └── DocumentDtos.cs                  # CHANGED: adds ExtractedTextPreviewDto
├── Scriptorium.API/
│   ├── Endpoints/
│   │   └── DocumentEndpoints.cs             # CHANGED: maps GET /{id}/text/preview
│   └── Program.cs                           # CHANGED: registers DocumentTextPreviewService
└── Scriptorium.Web/src/app/
    ├── documents/
    │   ├── document.models.ts               # CHANGED: adds TextPreview
    │   ├── document-api.service.ts          # CHANGED: adds getTextPreview(id)
    │   ├── text-popup/
    │   │   ├── text-popup-selection.service.ts   # NEW: which document's text is open (like ConversationSelectionService)
    │   │   ├── text-popup.component.ts           # NEW: the dialog, loads the preview, shows loading / error / text
    │   │   └── text-popup.component.html         # NEW (styles go in the shared src/styles.css, like the other components)
    │   └── document-list/
    │       └── document-list.component.{ts,html} # CHANGED: Text button on ready rows
    ├── app.component.{html,ts}                   # CHANGED: adds <app-text-popup />
    └── ../styles.css                             # CHANGED: popup, backdrop, cutting notice, scroll lock

tests/
├── Scriptorium.Core.Tests/
│   ├── Services/TextExcerptTests.cs, DocumentTextPreviewServiceTests.cs   # NEW; QuestionPromptBuilderTests stay as they are
│   ├── Results/TextPreviewResultTests.cs                                  # NEW
│   └── Dtos/                                                              # CHANGED: preview DTO mapping
└── Scriptorium.Infrastructure.Tests/Endpoints/                            # CHANGED: preview endpoint tests

README.md                                    # CHANGED: one line about the popup
```

**Structure Decision**: Follow exactly how feature 002 was built: a use-case service in Core with a result object and a failure
kind, a thin endpoint that maps the result to HTTP, a selection service plus a component in the Angular `documents` folder. The
cut moves into `TextExcerpt` so that "what the popup shows" and "what the model reads" come from one place (spec Assumptions).
The new endpoint sits beside `/text` rather than changing it, because `/text` is the full-text contract of feature 001 and is
still used to read a whole document.

## Complexity Tracking

No constitution violations to justify.
