# API Contract: Asking questions

Added to the documents API of feature 001 ([../../001-document-upload-parsing/contracts/documents-api.md](../../001-document-upload-parsing/contracts/documents-api.md)).
All endpoints are local-only (no auth, per project v1.0 scope). Error bodies use the existing shape
`{ "error": "..." }`.

## POST /api/documents/{id}/ask

Ask a question about a document. The server keeps no conversation: earlier exchanges are sent back in
`history` with every question.

**Request**: `application/json`

```json
{
  "question": "Who signed the contract?",
  "history": [
    { "question": "What is the contract about?", "answer": "It is a rental agreement for ..." }
  ]
}
```

- `question` (required): 1 to 2,000 characters after trimming.
- `history` (optional, default empty): earlier exchanges of the same conversation, oldest first. Only the
  most recent ones fit the model's window; older ones are ignored.

**Responses**:

- `200 OK`
  ```json
  { "answer": "The contract was signed by ...", "truncated": false }
  ```
  `truncated` is `true` when the document was longer than the model can read at once and only its
  beginning was used; the UI must then show a notice (FR-008). When the document does not contain the
  answer, `answer` says so (FR-003).
- `400 Bad Request` — empty question, question longer than allowed, or malformed body
  ```json
  { "error": "The question is empty." }
  ```
- `404 Not Found` — no document with that id (also returned when it was deleted during the conversation)
  ```json
  { "error": "Document not found." }
  ```
- `409 Conflict` — the document is not ready
  ```json
  { "error": "The document is not ready. Current status: processing." }
  ```
- `503 Service Unavailable` — the answering model cannot be reached or is not installed
  ```json
  { "error": "The answering model is not available. Check that Ollama is running and the model is installed." }
  ```
- `504 Gateway Timeout` — no answer within the configured time
  ```json
  { "error": "The model did not answer in time. Please try again." }
  ```
- `500 Internal Server Error` — anything unexpected; generic body, no details

Corresponds to spec FR-001 to FR-003, FR-006, FR-008 to FR-010 and FR-012; User Stories 1 to 3.
No document text or question is sent anywhere except to the model configured in `Ollama:BaseUrl` (FR-004).
