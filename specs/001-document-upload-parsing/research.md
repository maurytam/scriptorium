# Phase 0 Research: Document Upload and Parsing

## 1. Background text-extraction model

**Decision**: Accept the upload synchronously (validate type/size, persist the original
file and a `Document` row with status `processing`), then hand the file off to an
in-process background queue (a bounded `System.Threading.Channels.Channel<T>` consumed by a
single `BackgroundService`) that performs extraction and updates the `Document` row to
`ready` or `failed` when done. The HTTP response returns as soon as the row is created.

**Rationale**: Constitution Principle II (Async-First) forbids blocking the request thread,
and SC-006 only requires the document to reach a final status within 30 seconds of upload
finishing — not within the HTTP request itself. A channel-backed `BackgroundService` is
built into ASP.NET Core (no new NuGet package, per the Non-Negotiable Constraints), keeps
processing off the request thread, and is simple enough for a single-user local tool.

**Alternatives considered**:
- *Synchronous extraction inside the request*: simplest, but a large PDF could block the
  request for the full 30-second budget and contradicts Async-First's intent to avoid
  blocking under concurrent requests.
- *External job queue (Hangfire, Quartz.NET, etc.)*: adds a new NuGet dependency and
  operational complexity (scheduler, persistence) that a single-user local tool doesn't need;
  rejected as disproportionate (Constitution VI — Simplicity).

## 2. Original file storage location

**Decision**: Store original uploaded files on the local filesystem under an app-data
directory (e.g. `~/.scriptorium/documents/{documentId}/{filename}`), with the `Document`
entity storing only the file path, not the bytes.

**Rationale**: Keeps the SQLite database small and fast to query/back up; large binary
blobs in SQLite degrade read/write performance. Matches the "local-first, files on disk"
mental model already implied by the project's local-only v1.0 scope.

**Alternatives considered**:
- *Store file bytes as a SQLite BLOB column*: simpler (one storage medium) but bloats the
  database file and slows down every query that touches the `Document` table; rejected.

## 3. Extracted text storage

**Decision**: Store extracted text as a `TEXT` column in a separate `ExtractedText` table in
SQLite, keyed by `DocumentId` (one-to-one), rather than as a separate file on disk.

**Rationale**: FR-004 requires extracted text to be "stored separately from the original
file, linked by document ID" — a separate table satisfies this while keeping text
queryable and small enough (plain text, not binary) that SQLite handles it well. This also
sets up later features (Q&A, summarization) to query text directly via EF Core rather than
re-reading files from disk.

**Alternatives considered**:
- *Extracted text as a sidecar file on disk (mirroring the original file's storage)*: avoids
  any DB size growth, but complicates future full-text query support for no real benefit at
  this scale; rejected as unnecessary complexity for a single-user tool.

## 4. Per-format parsing and failure detection

**Decision**: One `IDocumentParser` implementation per format, each returning
`Result<string>` (extracted text or a failure reason) rather than throwing:

- **PDF (PdfPig)**: iterate pages and concatenate extracted text; if PdfPig throws on
  encryption/corruption, or the concatenated text is empty/whitespace-only (image-only PDF),
  return a `Result` failure with a specific reason ("password-protected or encrypted PDF" /
  "corrupted or unreadable PDF" / "no extractable text — scanned/image-only PDF not
  supported").
- **Word (DocumentFormat.OpenXml, `.docx`)**: open the package and concatenate all
  paragraph text from the main document body; any `OpenXmlPackageException` (e.g. not a
  valid Open XML package) is caught and mapped to a "corrupted or unreadable Word document"
  failure reason.
- **Excel (DocumentFormat.OpenXml, `.xlsx`)**: iterate every worksheet and concatenate cell
  text values across all sheets (per spec Assumption on multi-sheet handling); the same
  `OpenXmlPackageException` handling applies for corrupted workbooks.
- **Plain text (`.txt`)**: read as UTF-8; only failure mode is an I/O error, mapped to a
  generic "unable to read file" reason.

**Rationale**: Keeps each parser a single-responsibility class (Constitution VI) and
ensures no raw exception ever crosses from `Infrastructure` parsing code into `Core`
(Constitution IV) — every parser boundary converts exceptions into an explicit `Result`
failure with a human-readable reason, directly satisfying FR-006 and FR-011.

**Alternatives considered**:
- *Let exceptions propagate and catch them centrally in the processing queue*: would violate
  Constitution IV (Result<T> pattern in Core) by making exceptions the primary failure
  signal, and would produce generic rather than format-specific failure reasons; rejected.

## 5. Upload transport / file size enforcement

**Decision**: Accept uploads via a `multipart/form-data` POST to the minimal API, using
ASP.NET Core's built-in `IFormFile` binding with `Microsoft.AspNetCore.Http.Features
.FormOptions.MultipartBodyLengthLimit` (and the endpoint's own explicit check) set to 50 MB
(FR-002), rejecting oversized uploads before any parsing begins.

**Rationale**: `IFormFile` is part of ASP.NET Core itself (no new dependency) and is
sufficient at this scale (single-user, ≤50 MB files); streaming multipart parsing exists in
ASP.NET Core for very large uploads but is unnecessary complexity here.

**Alternatives considered**:
- *Manual streamed multipart parsing (`MultipartReader`)*: avoids fully buffering the file
  in memory, which matters for gigabyte-scale uploads, but is unnecessary complexity at the
  50 MB ceiling this feature enforces; rejected (Constitution VI — Simplicity).
