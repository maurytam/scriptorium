# Implementation Plan: Document Upload and Parsing

**Branch**: `001-document-upload-parsing` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-document-upload-parsing/spec.md`

## Summary

Users upload a PDF, Word, Excel, or plain-text document (≤50 MB) through the Angular web
UI. The API stores the original file and, asynchronously, extracts its text content into a
separate record linked by document ID. Each document tracks status (processing / ready /
failed) and metadata (filename, type, size, upload date, `IsPrivate`). Parsing failures are
caught per-format and surfaced as a "failed" status with a human-readable reason instead of
crashing the app. This is also the first feature implemented in the repository, so it
establishes the initial Clean Architecture project skeleton (`Scriptorium.Core`,
`Scriptorium.Infrastructure`, `Scriptorium.API`, `Scriptorium.Web`) described in `CLAUDE.md`.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: ASP.NET Core 10 minimal API (`Scriptorium.API`), Entity Framework
Core 10 with the SQLite provider (`Scriptorium.Infrastructure`), PdfPig (PDF text
extraction), DocumentFormat.OpenXml (Word/Excel text extraction), Angular (`Scriptorium.Web`)
for the upload UI and document list

**Storage**: SQLite database for `Document` and `ExtractedText` records; original uploaded
files persisted to a local filesystem folder (path referenced from the `Document` record) —
no cloud storage, consistent with the project's v1.0 local-first scope

**Testing**: xUnit + FluentAssertions + Moq, one test class per production class
(`Scriptorium.Core.Tests`, `Scriptorium.Infrastructure.Tests`)

**Target Platform**: Local macOS web app — self-hosted ASP.NET Core backend + Angular
frontend, optionally via Docker Compose; single user, single local instance

**Project Type**: Web application (Angular frontend + ASP.NET Core backend). No source
projects exist yet in this repository — this feature also bootstraps the solution skeleton.

**Performance Goals**: A typical (single-digit-MB) document reaches a final status
("ready"/"failed") within 30 seconds of upload completing (SC-006); the upload UI shows
progress without blocking the browser

**Constraints**: 50 MB max file size (FR-002); parsing MUST run asynchronously off the
request thread (Constitution II — Async-First) so a large document does not block other
requests; parsing failures MUST NOT crash the app or leave a document stuck in "processing"
(FR-006); no OCR, no multi-file batch upload, no Q&A/summarization/entity extraction in this
feature

**Scale/Scope**: Single local user/instance; 4 user stories, 11 functional requirements;
establishes the `Document`/`ExtractedText` persistence model that later features (Q&A,
summarization, entity extraction) will build on

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Clean Architecture | `IDocumentParser` and `IDocumentRepository` defined in `Scriptorium.Core`; PdfPig/OpenXml/EF Core usage confined to `Scriptorium.Infrastructure` implementations | PASS |
| II. Async-First | Upload, parsing, and repository calls are `async`/`await` end-to-end; parsing runs on a background queue, never via `.Result`/`.Wait()` | PASS |
| III. Nullable Reference Safety | New projects created with nullable reference types enabled; no `!` without justification | PASS |
| IV. Result<T> | `Core` parsing/upload operations return `Result<T>` (e.g. parse failure reason) instead of throwing across layers | PASS |
| V. Constructor Injection | `IDocumentParser` implementations, `IDocumentRepository`, and the upload/processing service are all constructor-injected; no service locator | PASS |
| VI. Single Responsibility & Simplicity | One parser class per format (PDF/DOCX/XLSX/TXT), each under 30 lines per method; a small orchestrating service coordinates them | PASS |
| VII. Provider Isolation | This feature does not call `IAIProvider`/`ModelRouter` at all — it only persists the `IsPrivate` flag for later features to use | PASS (not applicable) |
| VIII. Test Parity | Every new parser, service, and repository class gets a matching xUnit/FluentAssertions/Moq test class | PASS |

No violations identified. Complexity Tracking table is omitted (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-parsing/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   └── documents-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

This feature creates the initial solution skeleton described in `CLAUDE.md`. No existing
projects are modified; all paths below are new.

```text
src/
├── Scriptorium.Core/
│   ├── Entities/
│   │   ├── Document.cs
│   │   └── ExtractedText.cs
│   ├── Enums/
│   │   └── DocumentStatus.cs
│   ├── Interfaces/
│   │   ├── IDocumentParser.cs
│   │   └── IDocumentRepository.cs
│   ├── Results/
│   │   └── Result.cs
│   └── Services/
│       └── DocumentUploadService.cs
│
├── Scriptorium.Infrastructure/
│   ├── Parsing/
│   │   ├── PdfDocumentParser.cs
│   │   ├── WordDocumentParser.cs
│   │   ├── ExcelDocumentParser.cs
│   │   └── TextDocumentParser.cs
│   ├── Persistence/
│   │   ├── ScriptoriumDbContext.cs
│   │   └── DocumentRepository.cs
│   ├── Storage/
│   │   └── LocalFileStore.cs
│   └── Processing/
│       └── DocumentProcessingQueue.cs
│
├── Scriptorium.API/
│   └── Endpoints/
│       └── DocumentEndpoints.cs
│
└── Scriptorium.Web/
    └── src/app/documents/
        ├── upload/
        └── document-list/

tests/
├── Scriptorium.Core.Tests/
│   └── Services/
│       └── DocumentUploadServiceTests.cs
└── Scriptorium.Infrastructure.Tests/
    ├── Parsing/
    │   ├── PdfDocumentParserTests.cs
    │   ├── WordDocumentParserTests.cs
    │   ├── ExcelDocumentParserTests.cs
    │   └── TextDocumentParserTests.cs
    └── Persistence/
        └── DocumentRepositoryTests.cs
```

**Structure Decision**: Web application layout matching the Clean Architecture split already
defined in `CLAUDE.md` (`Scriptorium.Core` / `Infrastructure` / `API` / `Web`). Since no
projects exist yet, `/speckit-tasks` will include the project-scaffolding steps
(`dotnet new classlib`/`webapi`, adding them to `scriptorium.slnx`) ahead of the
feature-specific classes listed above.

## Complexity Tracking

*No Constitution Check violations — this section intentionally left empty.*
