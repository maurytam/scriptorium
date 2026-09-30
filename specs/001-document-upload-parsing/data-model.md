# Phase 1 Data Model: Document Upload and Parsing

## Document

Represents an uploaded file and its processing state. Defined in `Scriptorium.Core`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key, generated on upload |
| `FileName` | `string` | Original filename as uploaded, e.g. `report.pdf` |
| `FileType` | `string` | Normalized format: `pdf` \| `docx` \| `xlsx` \| `txt` |
| `FileSizeBytes` | `long` | Size of the original file; MUST be ≤ 52,428,800 (50 MB, FR-002) |
| `StoragePath` | `string` | Local filesystem path to the retained original file (FR-005) |
| `UploadDate` | `DateTimeOffset` | Set when the upload is accepted |
| `IsPrivate` | `bool` | Defaults to `false` when not specified at upload (FR-007) |
| `Status` | `DocumentStatus` | `Processing` \| `Ready` \| `Failed` (FR-008) |
| `FailureReason` | `string?` | Human-readable reason; set only when `Status == Failed` (FR-006, FR-011); `null` otherwise |

**Validation rules**:
- `FileType` MUST be one of the four supported values; anything else is rejected before a
  `Document` row is created (FR-001, FR-002).
- `FileSizeBytes` MUST be ≤ 50 MB; oversized files are rejected before a `Document` row is
  created (FR-002).
- `FailureReason` MUST be non-empty when `Status` is `Failed`, and MUST be `null` for
  `Processing`/`Ready` (keeps the "visible reason" requirement enforceable at the type
  level via the `Result<T>` pattern used to construct failure transitions).

**State transitions** (`DocumentStatus`):

```text
(upload accepted) -> Processing -> Ready    (extraction succeeded)
                                -> Failed   (extraction failed; FailureReason set)
```

`Processing` is the only valid initial state. `Ready` and `Failed` are terminal for this
feature — no retry/re-processing flow is in scope (see spec Assumptions).

## ExtractedText

Represents the plain-text content extracted from a `Document`. Defined in
`Scriptorium.Core`. Stored in a separate table so it can be retrieved (or queried) without
touching the original file (FR-004).

| Field | Type | Notes |
|---|---|---|
| `DocumentId` | `Guid` | Primary key and foreign key to `Document.Id` (one-to-one) |
| `Content` | `string` | Concatenated extracted text (all pages/paragraphs/sheets) |
| `ExtractedAt` | `DateTimeOffset` | Set when extraction completes successfully |

**Relationship**: One `Document` has at most one `ExtractedText` row, created only when
`Document.Status` transitions to `Ready`. A `Document` with `Status == Failed` or
`Processing` has no corresponding `ExtractedText` row.

## Notes for Infrastructure implementation (not part of the domain model)

- `ScriptoriumDbContext` (EF Core, SQLite) maps both entities; `ExtractedText.DocumentId` is
  both PK and FK, enforcing the one-to-one relationship at the schema level.
- The original file's bytes are never loaded into `Document` itself — only `StoragePath` is
  persisted, per the research.md decision to keep large binaries off the database.
