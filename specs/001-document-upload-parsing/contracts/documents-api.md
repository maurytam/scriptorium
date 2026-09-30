# API Contract: Documents

Minimal API endpoints exposed by `Scriptorium.API` for `Scriptorium.Web` (Angular) to call.
All endpoints are local-only (no auth, per project v1.0 scope).

## POST /api/documents

Upload a new document.

**Request**: `multipart/form-data`
- `file` (required): the document, one of `.pdf`, `.docx`, `.xlsx`, `.txt`, ≤ 50 MB
- `isPrivate` (optional, boolean, default `false`)

**Responses**:
- `202 Accepted` — file accepted, processing started
  ```json
  {
    "id": "guid",
    "fileName": "report.pdf",
    "fileType": "pdf",
    "fileSizeBytes": 123456,
    "uploadDate": "2026-08-03T12:00:00Z",
    "isPrivate": false,
    "status": "processing"
  }
  ```
- `400 Bad Request` — unsupported file type or missing file
  ```json
  { "error": "Unsupported file type '.png'. Supported types: pdf, docx, xlsx, txt." }
  ```
- `413 Payload Too Large` — file exceeds 50 MB
  ```json
  { "error": "File exceeds the maximum size of 50 MB." }
  ```

Corresponds to spec FR-001, FR-002, FR-003, FR-007; User Stories 1–3.

## GET /api/documents

List all uploaded documents, most recent first.

**Response**: `200 OK`
```json
[
  {
    "id": "guid",
    "fileName": "report.pdf",
    "fileType": "pdf",
    "uploadDate": "2026-08-03T12:00:00Z",
    "status": "ready"
  }
]
```

Corresponds to spec FR-009, FR-010; User Story 4.

## GET /api/documents/{id}

Get a single document's full metadata, including its failure reason if applicable.

**Responses**:
- `200 OK`
  ```json
  {
    "id": "guid",
    "fileName": "report.pdf",
    "fileType": "pdf",
    "fileSizeBytes": 123456,
    "uploadDate": "2026-08-03T12:00:00Z",
    "isPrivate": false,
    "status": "failed",
    "failureReason": "Password-protected or encrypted PDF"
  }
  ```
- `404 Not Found` — no document with that ID

Corresponds to spec FR-006, FR-008, FR-010, FR-011; supports the "visible reason" acceptance
scenario in User Story 2.

## GET /api/documents/{id}/text

Get the extracted text content for a document that has finished processing.

**Responses**:
- `200 OK`
  ```json
  { "documentId": "guid", "content": "extracted text...", "extractedAt": "2026-08-03T12:00:05Z" }
  ```
- `404 Not Found` — no document with that ID
- `409 Conflict` — document exists but is not yet `ready` (still `processing`) or is
  `failed` (no extracted text exists)
  ```json
  { "error": "Document is not ready. Current status: processing." }
  ```

Corresponds to spec FR-004; supports the independent-test acceptance scenario in User Story
1 ("the extracted text is retrievable by the document's unique identifier independently of
the original file"). Not used for Q&A/summarization in this feature — those are separate,
out-of-scope features that will consume this same endpoint or the underlying repository.
