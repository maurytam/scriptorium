# Contract: Text preview endpoint

One new endpoint. The existing `GET /api/documents/{id}/text` (feature 001) is unchanged and still returns the whole text.

## GET /api/documents/{id}/text/preview

Get the extracted text of a ready document, cut at the limit the model reads when answering questions
(`Qa:MaxDocumentCharacters`, 8,000 characters by default).

**Responses**:

- `200 OK`
  ```json
  {
    "documentId": "guid",
    "content": "the extracted text, or its first characters",
    "truncated": false,
    "totalCharacters": 1234
  }
  ```
  - `content` is the whole text when `totalCharacters` is not above the limit, and `truncated` is `false`.
  - Otherwise `content` is the first characters of the text (never ending on half of a character, so it can be one unit shorter than
    the limit), `truncated` is `true` and `totalCharacters` is the length of the whole text.
  - For a signed PDF the content starts with the digital-signature note (feature 003), exactly as in `/text`.
- `404 Not Found` — no document with that id
- `409 Conflict` — the document exists but is `processing` or `failed`
  ```json
  { "error": "Document is not ready. Current status: processing." }
  ```
  or, in the unexpected case of a ready document with no stored text, `{ "error": "The extracted text of this document is not available." }`

**Notes**:
- Read-only: the call changes nothing and does not touch the question flow.
- The cut text is exactly the first `content.length` characters of what `/text` returns for the same document.
- The response size is bounded by the limit, whatever the size of the document.

Corresponds to FR-002, FR-005, FR-006, FR-007, FR-008 and FR-010 of the spec.
