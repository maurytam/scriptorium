# Feature Specification: Document Upload and Parsing

**Feature Branch**: `001-document-upload-parsing`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "Document upload and parsing

Users can upload a document (PDF, Word, or Excel) through the web UI. The system extracts the text content and stores both the original file and the extracted text so it can later be used for Q&A, summarization, and entity extraction.

User stories:
- As a user, I can select a file from my computer and upload it, so I can start working with its content.
- As a user, I see the upload progress and get a clear error if the file type is unsupported or the file is corrupted.
- As a user, I can mark a document as private (IsPrivate flag) at upload time, so sensitive content is later routed only to the local model.
- As a user, I can see a list of my uploaded documents with name, type, upload date, and status (processing / ready / failed).

Functional requirements:
- Supported formats: PDF, .docx, .xlsx, .txt
- Max file size: 50 MB
- Extracted text must be stored separately from the original file, linked by document ID
- Parsing failures must not crash the app — document status becomes \"failed\" with a visible reason
- Each document has metadata: filename, type, size, upload date, IsPrivate, status

Out of scope for this feature:
- Q&A, summarization, entity extraction (separate features)
- OCR for scanned/image-only PDFs
- Multi-file batch upload"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload a document (Priority: P1)

A user selects a document file (PDF, Word, Excel, or plain text) from their computer and
uploads it through the web UI so they can start working with its content.

**Why this priority**: This is the entry point for every other capability in Scriptorium —
without a successful upload, there is no document to summarize, question, or extract
entities from. It is the minimum slice that delivers standalone value.

**Independent Test**: Can be fully tested by uploading a valid PDF, .docx, .xlsx, or .txt
file and confirming the document is stored, its text is extracted, and its status
becomes "ready".

**Acceptance Scenarios**:

1. **Given** the user is on the upload screen, **When** they select a valid PDF, .docx,
   .xlsx, or .txt file within the size limit and confirm the upload, **Then** the file is
   stored, its text content is extracted and stored separately, and the document's status
   becomes "ready".
2. **Given** a document has just finished uploading, **When** the system finishes
   extracting its text, **Then** the extracted text is retrievable by the document's unique
   identifier independently of the original file.

---

### User Story 2 - See upload progress and clear errors (Priority: P2)

A user sees visible progress while a file is uploading and processing, and receives a
clear, specific error message if the file type is unsupported or the file is corrupted.

**Why this priority**: Trust and usability depend on the user knowing what's happening and
why something failed, rather than facing a silent hang or a crash.

**Independent Test**: Can be fully tested by uploading a file of an unsupported type (e.g.
`.png`) and a corrupted file of a supported type (e.g. a `.docx` with invalid internal
structure), and confirming both produce a specific, visible error rather than a crash or
indefinite "processing" state.

**Acceptance Scenarios**:

1. **Given** a user starts uploading a file, **When** the upload and processing are in
   progress, **Then** the user sees a visible progress/status indicator until the document
   reaches "ready" or "failed".
2. **Given** a user uploads a file with an unsupported extension, **When** the upload is
   submitted, **Then** the system rejects it immediately with a clear message naming the
   supported formats.
3. **Given** a user uploads a file with a supported extension but corrupted or
   unparseable content, **When** the system attempts to extract its text, **Then** the
   document's status becomes "failed" with a human-readable reason, and the rest of the
   application continues to function normally.

---

### User Story 3 - Mark a document as private at upload time (Priority: P3)

A user marks a document as private (`IsPrivate` flag) while uploading it, so that
sensitive content is later routed only to the local model rather than a cloud AI service.

**Why this priority**: Privacy control is a core promise of Scriptorium, but it builds on
top of a working upload flow — the flag has no effect until User Story 1 exists.

**Independent Test**: Can be fully tested by uploading a document with the private option
checked and confirming the stored document record has `IsPrivate = true`, visible in the
document's metadata/list entry.

**Acceptance Scenarios**:

1. **Given** a user is uploading a document, **When** they enable the "private" option
   before confirming the upload, **Then** the resulting document record has
   `IsPrivate = true`.
2. **Given** a user uploads a document without enabling the "private" option, **When** the
   upload completes, **Then** the resulting document record has `IsPrivate = false` by
   default.

---

### User Story 4 - View list of uploaded documents (Priority: P4)

A user sees a list of their uploaded documents showing name, type, upload date, and status
(processing / ready / failed), so they can track what has been uploaded and its current
state.

**Why this priority**: This closes the loop on the upload flow, letting users confirm
success or spot failures, but it is a read-only view that depends on documents already
existing from the prior stories.

**Independent Test**: Can be fully tested by uploading one or more documents and confirming
each appears in the list with the correct filename, type, upload date, and status, and
that status updates as processing completes.

**Acceptance Scenarios**:

1. **Given** one or more documents have been uploaded, **When** the user opens the
   document list, **Then** each document is shown with its filename, type, upload date,
   and current status.
2. **Given** a document is still being processed, **When** the user views the list,
   **Then** that document shows a "processing" status; once extraction finishes it updates
   to "ready" or "failed" without requiring the user to re-upload.

---

### Edge Cases

- What happens when a file exceeds the 50 MB size limit? System rejects the upload before
  processing begins and shows a clear message stating the limit.
- What happens when the upload is interrupted mid-transfer (e.g. network drop)? The
  document is marked "failed" with a reason indicating the upload did not complete, rather
  than being left in "processing" indefinitely.
- What happens when a supported file type contains no extractable text (e.g. an
  image-only/scanned PDF)? Since OCR is out of scope, the document is marked "failed" with
  a reason indicating no text could be extracted.
- What happens when a PDF is password-protected or encrypted? Parsing fails and the
  document is marked "failed" with a reason indicating the file is protected.
- What happens when an Excel file has multiple sheets? Text from all sheets is extracted
  and stored as the document's extracted text.
- What happens when two uploaded documents share the same filename? Each is stored as a
  distinct document with its own unique identifier and appears as a separate entry in the
  list.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a user to upload a single document file in PDF, .docx,
  .xlsx, or .txt format through the web UI.
- **FR-002**: System MUST reject files that exceed 50 MB and files with unsupported
  extensions, in both cases showing a clear message before processing begins.
- **FR-003**: System MUST display upload/processing progress to the user from the start of
  the upload until the document reaches a final status ("ready" or "failed").
- **FR-004**: System MUST extract the text content of an uploaded document and store it
  separately from the original file, linked to the same document via a unique document
  identifier.
- **FR-005**: System MUST retain the original uploaded file regardless of whether text
  extraction succeeds or fails.
- **FR-006**: System MUST NOT crash or become unusable when parsing fails; instead it MUST
  set the document's status to "failed" and record a human-readable reason.
- **FR-007**: System MUST allow the user to mark a document as private (`IsPrivate = true`)
  at upload time; when not specified, `IsPrivate` defaults to `false`.
- **FR-008**: System MUST track and expose each document's status as one of "processing",
  "ready", or "failed" at all times after upload begins.
- **FR-009**: System MUST provide a list of all uploaded documents showing filename, file
  type, upload date, and current status for each.
- **FR-010**: System MUST record the following metadata for every uploaded document:
  filename, file type, file size, upload date, `IsPrivate` flag, and status.
- **FR-011**: System MUST treat documents with no extractable text (e.g. image-only content
  or password-protected files) as parsing failures rather than silently producing an empty
  result.

### Key Entities

- **Document**: Represents an uploaded file. Key attributes: unique identifier, original
  filename, file type, file size, upload date/time, `IsPrivate` flag, status (processing /
  ready / failed), and failure reason (when status is "failed"). One Document has exactly
  one associated extracted-text record once processing succeeds.
- **Extracted Text**: Represents the plain-text content pulled from a Document. Key
  attributes: reference to its owning Document's unique identifier, the extracted text
  content, and extraction timestamp. Stored separately from the original file so the two
  can be retrieved independently.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from selecting a valid file to seeing it listed with "ready"
  status without needing to refresh the page or restart the application.
- **SC-002**: 100% of uploads with an unsupported file type or size over the 50 MB limit
  are rejected immediately with a clear, specific message, with zero application crashes.
- **SC-003**: 100% of uploads with corrupted or unparseable supported-format files result in
  a "failed" status with a visible, human-readable reason, with zero application crashes.
- **SC-004**: A user can determine the processing state (processing / ready / failed) of
  any uploaded document at a glance from the document list, without opening the document.
- **SC-005**: A user can mark a document as private in the same action as uploading it, and
  that choice is visibly confirmed afterward (e.g. in the document list or detail view).
- **SC-006**: A typical document under the 50 MB limit completes processing and reaches a
  final status ("ready" or "failed") within 30 seconds of the upload finishing.

## Assumptions

- The 50 MB maximum file size is a reasonable default for a local-first, single-user tool;
  it can be revisited if real-world documents routinely exceed it.
- A target of 30 seconds to reach a final status for a typical (single-digit-MB) document is
  a reasonable default performance expectation; no explicit SLA was provided.
- Password-protected/encrypted PDFs and image-only (scanned) PDFs are treated as parsing
  failures in this feature, consistent with OCR being explicitly out of scope.
- For Excel files, all sheets' text content is extracted and combined into the document's
  extracted text; selecting specific sheets is not supported in this feature.
- Consistent with the product's v1.0 local-first, single-user scope, the document list
  shows all documents in the local instance — there is no per-user ownership or filtering.
- Deleting or removing an uploaded document is not covered by this feature.
- Asking questions about a document, summarizing it, extracting entities from it, and
  batch-uploading multiple files at once are explicitly out of scope for this feature and
  will be addressed by separate features.
