---

description: "Task list for Document Upload and Parsing"

---

# Tasks: Document Upload and Parsing

**Input**: Design documents from `/specs/001-document-upload-parsing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/documents-api.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity — every production class has a matching test class, xUnit + FluentAssertions + Moq). This is a project-wide non-negotiable, not an optional extra.

**Organization**: Tasks are grouped by user story (from spec.md) to enable independent implementation and testing of each story. No source projects exist yet in this repository, so Phase 1 also scaffolds the solution described in `CLAUDE.md`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Web app layout per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.Infrastructure/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/` (Angular), `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the solution — no projects exist yet in this repository

- [X] T001 Create `Scriptorium.Core` (classlib), `Scriptorium.Infrastructure` (classlib), and `Scriptorium.API` (webapi, minimal APIs) projects under `src/`, and add all three to `scriptorium.slnx`
- [X] T002 [P] Initialize the Angular workspace in `src/Scriptorium.Web/` per `plan.md`'s Project Structure
- [X] T003 [P] Create `Scriptorium.Core.Tests` and `Scriptorium.Infrastructure.Tests` xUnit projects under `tests/`, add xUnit, FluentAssertions, and Moq package references, and add both to `scriptorium.slnx`
- [X] T004 [P] Enable nullable reference types (`<Nullable>enable</Nullable>`) in every new `.csproj` under `src/` and `tests/` (Constitution III)
- [X] T005 [P] Add `PdfPig` and `DocumentFormat.OpenXml` package references to `src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj`
- [X] T006 [P] Add `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` package references to `src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj`
- [X] T007 Add project references: `Scriptorium.Infrastructure` → `Scriptorium.Core`; `Scriptorium.API` → `Scriptorium.Core` and `Scriptorium.Infrastructure`; `Scriptorium.Core.Tests` → `Scriptorium.Core`; `Scriptorium.Infrastructure.Tests` → `Scriptorium.Infrastructure`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain types, parsers, persistence, storage, and the background processing pipeline — every user story below depends on these being in place

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 [P] Create `DocumentStatus` enum (`Processing`, `Ready`, `Failed`) in `src/Scriptorium.Core/Enums/DocumentStatus.cs`
- [X] T009 [P] Create the `Result<T>` type in `src/Scriptorium.Core/Results/Result.cs` (Constitution IV — no raw exceptions across layer boundaries)
- [X] T010 [P] Create the `Document` entity in `src/Scriptorium.Core/Entities/Document.cs` per `data-model.md` (Id, FileName, FileType, FileSizeBytes, StoragePath, UploadDate, IsPrivate, Status, FailureReason)
- [X] T011 [P] Create the `ExtractedText` entity in `src/Scriptorium.Core/Entities/ExtractedText.cs` per `data-model.md` (DocumentId, Content, ExtractedAt)
- [X] T012 [P] Define `IDocumentParser` in `src/Scriptorium.Core/Interfaces/IDocumentParser.cs` (`SupportedFileType` + `Task<Result<string>> ExtractTextAsync(Stream content, CancellationToken ct)`)
- [X] T013 [P] Define `IDocumentRepository` in `src/Scriptorium.Core/Interfaces/IDocumentRepository.cs` (`AddAsync`, `GetByIdAsync`, `UpdateStatusAsync`, `SaveExtractedTextAsync`)
- [X] T014 [P] Implement `PdfDocumentParser` in `src/Scriptorium.Infrastructure/Parsing/PdfDocumentParser.cs` using PdfPig; return `Result` failures for encrypted/corrupted/no-extractable-text PDFs per `research.md` §4
- [X] T015 [P] Implement `WordDocumentParser` in `src/Scriptorium.Infrastructure/Parsing/WordDocumentParser.cs` using DocumentFormat.OpenXml; return a `Result` failure on `OpenXmlPackageException`
- [X] T016 [P] Implement `ExcelDocumentParser` in `src/Scriptorium.Infrastructure/Parsing/ExcelDocumentParser.cs` using DocumentFormat.OpenXml, concatenating text across all worksheets
- [X] T017 [P] Implement `TextDocumentParser` in `src/Scriptorium.Infrastructure/Parsing/TextDocumentParser.cs` reading UTF-8 content
- [X] T018 [P] Unit tests for `PdfDocumentParser` in `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfDocumentParserTests.cs` (valid, corrupted, encrypted, image-only PDFs)
- [X] T019 [P] Unit tests for `WordDocumentParser` in `tests/Scriptorium.Infrastructure.Tests/Parsing/WordDocumentParserTests.cs` (valid and corrupted `.docx`)
- [X] T020 [P] Unit tests for `ExcelDocumentParser` in `tests/Scriptorium.Infrastructure.Tests/Parsing/ExcelDocumentParserTests.cs` (valid multi-sheet and corrupted `.xlsx`)
- [X] T021 [P] Unit tests for `TextDocumentParser` in `tests/Scriptorium.Infrastructure.Tests/Parsing/TextDocumentParserTests.cs`
- [X] T022 Create `ScriptoriumDbContext` in `src/Scriptorium.Infrastructure/Persistence/ScriptoriumDbContext.cs` mapping `Document` and `ExtractedText` per `data-model.md` (depends on T010, T011)
- [X] T023 Generate the initial EF Core migration (`dotnet ef migrations add InitialCreate --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API`) (depends on T022)
- [X] T024 Implement `DocumentRepository` in `src/Scriptorium.Infrastructure/Persistence/DocumentRepository.cs` implementing `IDocumentRepository` (depends on T013, T022)
- [X] T025 [P] Unit tests for `DocumentRepository` in `tests/Scriptorium.Infrastructure.Tests/Persistence/DocumentRepositoryTests.cs` (SQLite in-memory provider)
- [X] T026 Implement `LocalFileStore` in `src/Scriptorium.Infrastructure/Storage/LocalFileStore.cs`, saving original file bytes under the app-data directory and returning the storage path, per `research.md` §2
- [X] T027 [P] Unit tests for `LocalFileStore` in `tests/Scriptorium.Infrastructure.Tests/Storage/LocalFileStoreTests.cs`
- [X] T028 Implement `DocumentProcessingQueue` (bounded `Channel<Guid>` + `BackgroundService`) in `src/Scriptorium.Infrastructure/Processing/DocumentProcessingQueue.cs`: dequeues a document id, resolves the matching `IDocumentParser` by file type, calls `ExtractTextAsync`, and updates the `Document` to `Ready` (+ saves `ExtractedText`) or `Failed` (+ reason) via `IDocumentRepository`, per `research.md` §1 (depends on T012, T013, T014–T017, T024)
- [X] T029 [P] Unit tests for `DocumentProcessingQueue` in `tests/Scriptorium.Infrastructure.Tests/Processing/DocumentProcessingQueueTests.cs`, covering both the `Ready` and `Failed` transitions with mocked `IDocumentParser`/`IDocumentRepository`
- [X] T030 Register `ScriptoriumDbContext`, `IDocumentRepository`, all four `IDocumentParser` implementations, and `DocumentProcessingQueue` in `src/Scriptorium.API/Program.cs`'s DI container (Constitution V — constructor injection only)

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Upload a document (Priority: P1) 🎯 MVP

**Goal**: A user selects and uploads a valid PDF/.docx/.xlsx/.txt file; it is stored, its text is extracted into a separate record, and its status becomes "ready".

**Independent Test**: Upload a valid file of each supported type and confirm the document is stored, its extracted text is retrievable by document ID, and its status becomes "ready".

- [X] T031 [US1] Implement `DocumentUploadService` in `src/Scriptorium.Core/Services/DocumentUploadService.cs`: accepts a stream + filename + `isPrivate`, saves the file via `LocalFileStore`, creates a `Document` row with status `Processing`, enqueues it on `DocumentProcessingQueue`, and returns `Result<Document>` (happy path only) (depends on T009, T010, T013, T026, T028)
- [X] T032 [P] [US1] Unit tests for `DocumentUploadService` happy-path scenarios in `tests/Scriptorium.Core.Tests/Services/DocumentUploadServiceTests.cs`
- [X] T033 [US1] Implement `POST /api/documents` (happy path, `202 Accepted`) in `src/Scriptorium.API/Endpoints/DocumentEndpoints.cs` per `contracts/documents-api.md` (depends on T031)
- [X] T034 [US1] Implement `GET /api/documents/{id}` in `src/Scriptorium.API/Endpoints/DocumentEndpoints.cs` per `contracts/documents-api.md` (depends on T024)
- [X] T035 [US1] Implement `GET /api/documents/{id}/text` in `src/Scriptorium.API/Endpoints/DocumentEndpoints.cs` per `contracts/documents-api.md` (depends on T024)
- [X] T036 [P] [US1] Integration tests for the upload → ready flow (`WebApplicationFactory`) in `tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentUploadEndpointTests.cs`, covering all four supported formats
- [X] T037 [US1] Build the Angular upload component (file picker + submit) in `src/Scriptorium.Web/src/app/documents/upload/`, calling `POST /api/documents`
- [X] T038 [US1] Add status polling against `GET /api/documents/{id}` to the upload flow in `src/Scriptorium.Web/src/app/documents/upload/` until a terminal status is reached

**Checkpoint**: User Story 1 is fully functional and testable independently

---

## Phase 4: User Story 2 - See upload progress and clear errors (Priority: P2)

**Goal**: A user sees visible progress while uploading/processing, and gets a clear, specific error for unsupported or corrupted files.

**Independent Test**: Upload an unsupported file type and a corrupted supported-format file; confirm both produce a specific, visible error (not a crash or indefinite "processing").

- [X] T039 [US2] Extend `DocumentUploadService` (`src/Scriptorium.Core/Services/DocumentUploadService.cs`) to validate file extension and the 50 MB size limit before creating a `Document` row, returning a `Result` failure for each case
- [X] T040 [P] [US2] Extend `DocumentUploadServiceTests` (`tests/Scriptorium.Core.Tests/Services/DocumentUploadServiceTests.cs`) with unsupported-type and oversized-file cases
- [X] T041 [US2] Extend `POST /api/documents` (`src/Scriptorium.API/Endpoints/DocumentEndpoints.cs`) to map validation failures to `400 Bad Request` (unsupported type) and `413 Payload Too Large` (oversized), including `MultipartBodyLengthLimit` configuration
- [X] T042 [P] [US2] Integration tests for unsupported-type (400), oversized (413), and corrupted-file (`Failed` status + reason) scenarios in `tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentUploadEndpointTests.cs`
- [X] T043 [US2] Add an upload/processing progress indicator and error-message display to the Angular upload component in `src/Scriptorium.Web/src/app/documents/upload/`

**Checkpoint**: User Stories 1 AND 2 both work independently

---

## Phase 5: User Story 3 - Mark a document as private at upload time (Priority: P3)

**Goal**: A user marks a document private (`IsPrivate = true`) at upload time so it is later routed only to the local model.

**Independent Test**: Upload a document with the private option enabled and confirm the stored record has `IsPrivate = true`; upload without it and confirm `IsPrivate = false`.

- [X] T044 [US3] Extend `DocumentUploadService` (`src/Scriptorium.Core/Services/DocumentUploadService.cs`) and `POST /api/documents` (`src/Scriptorium.API/Endpoints/DocumentEndpoints.cs`) to accept and persist the `isPrivate` form field, defaulting to `false`
- [X] T045 [P] [US3] Unit tests confirming `IsPrivate` is persisted as provided and defaults to `false` in `tests/Scriptorium.Core.Tests/Services/DocumentUploadServiceTests.cs`
- [X] T046 [P] [US3] Integration test confirming `isPrivate=true` round-trips through `GET /api/documents/{id}` in `tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentUploadEndpointTests.cs`
- [X] T047 [US3] Add a "Mark as private" checkbox to the Angular upload component in `src/Scriptorium.Web/src/app/documents/upload/`

**Checkpoint**: User Stories 1, 2, AND 3 all work independently

---

## Phase 6: User Story 4 - View list of uploaded documents (Priority: P4)

**Goal**: A user sees a list of uploaded documents with name, type, upload date, and status.

**Independent Test**: Upload one or more documents and confirm each appears in the list with correct metadata and status, updating as processing completes.

- [X] T048 [US4] Add `GetAllAsync` to `IDocumentRepository` (`src/Scriptorium.Core/Interfaces/IDocumentRepository.cs`) and implement it in `DocumentRepository` (`src/Scriptorium.Infrastructure/Persistence/DocumentRepository.cs`), ordered by upload date descending
- [X] T049 [P] [US4] Unit test for `DocumentRepository.GetAllAsync` ordering in `tests/Scriptorium.Infrastructure.Tests/Persistence/DocumentRepositoryTests.cs`
- [X] T050 [US4] Implement `GET /api/documents` (list) in `src/Scriptorium.API/Endpoints/DocumentEndpoints.cs` per `contracts/documents-api.md` (depends on T048)
- [X] T051 [P] [US4] Integration test for the list endpoint in `tests/Scriptorium.Infrastructure.Tests/Endpoints/DocumentListEndpointTests.cs`
- [X] T052 [US4] Build the Angular document-list component in `src/Scriptorium.Web/src/app/documents/document-list/`, showing filename, type, upload date, and status, calling `GET /api/documents`

**Checkpoint**: All four user stories are independently functional

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T053 [P] Add global exception-handling middleware in `src/Scriptorium.API/Program.cs` so an unhandled exception returns a generic 500 instead of crashing the process (belt-and-braces for FR-006)
- [X] T054 [P] Add `Storage:LocalPath` and `Documents:MaxSizeBytes` configuration entries to `src/Scriptorium.API/appsettings.json` (backing T026, T039)
- [X] T055 Run the `quickstart.md` validation scenarios end-to-end against the running app and record results
- [X] T056 [P] Review all new classes against Constitution VI (methods under 30 lines, single responsibility) and refactor any that exceed it

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–6)**: All depend on Foundational completion; can proceed in parallel or in priority order (P1 → P2 → P3 → P4)
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories
- **User Story 2 (P2)**: Extends US1's `DocumentUploadService`/endpoint files but is independently testable via its own error-path scenarios
- **User Story 3 (P3)**: Extends US1's `DocumentUploadService`/endpoint files but is independently testable via the `IsPrivate` round-trip
- **User Story 4 (P4)**: Extends the Foundational `DocumentRepository` but is independently testable via the list endpoint alone

### Within Each User Story

- Service/repository logic before endpoints
- Endpoints before Angular UI
- Tests alongside (or immediately after) the task they cover

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- Within Foundational: entity/interface tasks (T008–T013) in parallel; the four parsers (T014–T017) in parallel; the four parser test files (T018–T021) in parallel
- Once Foundational completes, US1–US4 backend work can proceed in parallel by different developers, though US2/US3 touch the same files as US1 (`DocumentUploadService`, `DocumentEndpoints`) and should be sequenced if worked on by one person
- Each story's [P]-marked test tasks can run in parallel with that story's other tasks that touch different files

---

## Parallel Example: Foundational Phase

```bash
# Launch the four format parsers together (different files):
Task: "Implement PdfDocumentParser in src/Scriptorium.Infrastructure/Parsing/PdfDocumentParser.cs"
Task: "Implement WordDocumentParser in src/Scriptorium.Infrastructure/Parsing/WordDocumentParser.cs"
Task: "Implement ExcelDocumentParser in src/Scriptorium.Infrastructure/Parsing/ExcelDocumentParser.cs"
Task: "Implement TextDocumentParser in src/Scriptorium.Infrastructure/Parsing/TextDocumentParser.cs"

# Launch their tests together:
Task: "Unit tests for PdfDocumentParser in tests/Scriptorium.Infrastructure.Tests/Parsing/PdfDocumentParserTests.cs"
Task: "Unit tests for WordDocumentParser in tests/Scriptorium.Infrastructure.Tests/Parsing/WordDocumentParserTests.cs"
Task: "Unit tests for ExcelDocumentParser in tests/Scriptorium.Infrastructure.Tests/Parsing/ExcelDocumentParserTests.cs"
Task: "Unit tests for TextDocumentParser in tests/Scriptorium.Infrastructure.Tests/Parsing/TextDocumentParserTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run Scenario 1 from `quickstart.md`
5. Demo if ready

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add User Story 1 → validate → MVP demo (upload works end-to-end)
3. Add User Story 2 → validate → error handling demo
4. Add User Story 3 → validate → privacy flag demo
5. Add User Story 4 → validate → full feature demo

---

## Phase 8: Additions after review (duplicates, deletion, styling)

**Purpose**: Requirements added by the user after reviewing phases 1–6 (spec FR-012, FR-013). All done on the same branch as phase 6.

- [X] T057 Add `ContentHash` to `Document`, compute the SHA-256 in `DocumentUploadService` before storing anything, and reject duplicates with `UploadFailureKind.Duplicate` (`409`); add `IDocumentRepository.FindByContentHashAsync` and the `AddContentHash` EF migration
- [X] T058 [P] Tests for duplicate detection: `DocumentUploadServiceTests`, `UploadResultTests`, `DocumentRepositoryTests`, and endpoint tests in `DocumentUploadEndpointTests` (same content again, renamed copy, same name with different content, re-upload after deletion)
- [X] T059 Add `DocumentDeletionService`, `DeleteResult`/`DeleteFailureKind`, `IDocumentRepository.DeleteAsync`, `IFileStore.Delete` (`LocalFileStore`) and `DELETE /api/documents/{id}` (`204`/`404`/`409` while processing)
- [X] T060 [P] Tests for deletion: `DocumentDeletionServiceTests`, `DeleteResultTests`, `DocumentRepositoryTests`, `LocalFileStoreTests` and `DocumentDeleteEndpointTests`
- [X] T061 Angular: trash button on the right of each row with confirmation in the row, disabled while processing (`document-list`), `DocumentApiService.delete`, plus specs
- [X] T062 Restyle the UI: a single global stylesheet `src/Scriptorium.Web/src/styles.css` (parchment/ink/burgundy theme, dark mode, responsive); component stylesheets removed

---

## Notes

- [P] tasks touch different files with no unmet dependencies
- [Story] label maps a task to its user story for traceability
- US2/US3 tasks extend files created in US1 (`DocumentUploadService.cs`, `DocumentEndpoints.cs`) — expect small, additive diffs, not rewrites
- Every production class listed above has a paired test task per Constitution VIII
- Commit after each task or logical group; do not commit automatically without asking (Constitution — Non-Negotiable Constraints)
- Stop at any checkpoint to validate a story independently before moving to the next
