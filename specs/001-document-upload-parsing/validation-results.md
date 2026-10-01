# Validation results: Document Upload and Parsing

Results of the polish phase (tasks T055 and T056), run on 2026-10-01 from branch
`005-polish-and-validation` (based on `main` at `248204d`) with the .NET 10.0.100 SDK.

## 1. Quickstart scenarios (T055)

The eight scenarios of [quickstart.md](./quickstart.md) were run against the real API with `curl`.

**Environment**

- API started with `dotnet run` against a **throwaway database and folder**
  (`ConnectionStrings__Default` and `Storage__LocalPath` pointing to a temporary directory).
- It listened on port **5098** instead of 5034, because 5034 was in use by another running
  instance of the API. The commands are otherwise exactly those of the quickstart.
- Sample files were generated for the run: a small valid `.pdf`, `.docx`, `.xlsx` and `.txt`
  (each with different content), a `.png`, a renamed text file used as `corrupted.docx`, and
  two files of 52.9 MB and 62 MB.

| # | Scenario | What was run | Observed | Result |
|---|---|---|---|---|
| 1 | Valid upload (US1) | Upload of the `.pdf`, `.docx`, `.xlsx`, `.txt` | `202` for each; all reached `ready` in under a second; `GET /{id}/text` returned the expected text for each format | Pass |
| 2a | Unsupported type (US2) | Upload of a `.png` | `400` with `Unsupported file type '.png'. Supported types: pdf, docx, xlsx, txt.` | Pass |
| 2b | Corrupted file (US2) | Upload of the renamed text file as `.docx` | `202`, then `failed` with `Corrupted or unreadable Word document`; the API kept answering (`/limits` → `200`) | Pass |
| 3a | Oversized file | Upload of 52.9 MB | `413` with `File exceeds the maximum size of 50 MB.` | Pass |
| 3b | Oversized file | Upload of 62 MB | `413` with an empty body (refused by the web server before the application) | Pass, see note 1 |
| 3c | No side effects | Document and folder counts before and after 3a/3b | 5 → 5 documents, 5 → 5 stored folders | Pass |
| 4 | Private flag (US3) | Upload with `isPrivate=true`, and a different file without the flag | `GET` shows `isPrivate: true` and `isPrivate: false` respectively | Pass |
| 5 | List (US4) | `GET /api/documents` | 7 documents, most recent first, each with name, type, status, `isPrivate` and upload date | Pass |
| 6a | Duplicate (FR-012) | Same file again | `409` with `This file has already been uploaded as 'sample.pdf'.` | Pass |
| 6b | Duplicate (FR-012) | Renamed copy of the same file | `409`, same message naming the original | Pass |
| 6c | Same name, other content | A new `public.txt` with different content | `202`; the list grew by one entry | Pass |
| 7a | Delete (FR-013) | `DELETE` of a ready document | `204`; `GET` and `GET /text` then answer `404`; stored folders 8 → 7 | Pass |
| 7b | Delete unknown id | `DELETE` of a non-existent id | `404` with `Document not found.` | Pass |
| 7c | Re-upload after delete | Same content as the deleted document | `202` | Pass |
| 8 | Limits | `GET /api/documents/limits` | `200` with `{"maxSizeBytes":52428800}` | Pass |

The API log contained no `fail` or `crit` entries during the run.

**Notes**

1. Between 50 MB and about 51 MB the application answers with the JSON message; beyond that the
   web server itself refuses the request with an empty `413`. The web UI avoids both cases by
   checking the file size against `GET /api/documents/limits` before uploading.
2. Not reproducible by hand, covered by automated tests instead:
   - an unhandled server error answering `500` with a generic body and no details
     (`UnhandledExceptionEndpointTests`, `GlobalExceptionHandlerTests`);
   - deleting a document that is still `processing` answering `409`
     (`DocumentDeletionServiceTests`).

## 2. Review against Constitution VI (T056)

Principle VI: each class has one responsibility and methods stay under 30 lines.

**Method.** A script measured every method body in `src/` (excluding migrations): the number of
lines between the braces. Expression-bodied members count as one line. The figures are a
heuristic, good enough to find outliers, not a parser.

**Result: no method exceeds 30 lines.** The longest is `DocumentDeletionService.DeleteAsync` with 24.

| Class | Longest method | Responsibility | Verdict |
|---|---|---|---|
| `DocumentEndpoints` | 15 | HTTP mapping of the documents API (7 handlers) | OK |
| `GlobalExceptionHandler` | 16 | Turns unhandled exceptions into the JSON error shape | OK |
| `Program` | n/a (75 lines of top-level statements) | Composition root: configuration and service registration | Acceptable, see note |
| `DocumentDtos` (4 records) | 1 | Response shapes and their mapping from entities | OK |
| `Result`, `UploadResult`, `DeleteResult` | 1 | Outcome types | OK |
| `DocumentDeletionService` | 24 | One use case: delete a document and its file | OK |
| `DocumentUploadService` | 21 | Orchestrates validation, hashing, duplicate check, storage, persistence and queueing | OK, see note |
| `PdfDocumentParser`, `WordDocumentParser`, `ExcelDocumentParser`, `TextDocumentParser` | 10 to 17 | One format each | OK |
| `DocumentRepository` | 13 | Persistence of documents and extracted text (9 members) | OK |
| `ScriptoriumDbContext` | 10 | Model mapping, split in one method per entity | OK |
| `DocumentProcessingQueue` | 19 | Background queue and processing pipeline | OK, see note |
| `LocalFileStore` | 16 | Original files on disk | OK |

**Observations (no change made)**

- `Program.cs` is long but is only wiring. If it grows, the registrations can move to extension methods.
- `DocumentUploadService` has the widest scope: its inspection step (type, size, hash, duplicate
  check) could become a class of its own if more rules are added.
- `DocumentProcessingQueue` combines the queue and the processing pipeline; worth splitting when
  later features add steps such as AI calls.
- `DocumentRepository` also holds the extracted-text operations; a second repository would make
  sense if conversations or other entities are added.

No refactoring was requested for these, so none was done.
