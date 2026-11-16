# API Contract: Summarising a document

New endpoint. Everything about preferences, backends and refusals is the same as for questions, see
[../../005-model-router/contracts/ask-routing-api.md](../../005-model-router/contracts/ask-routing-api.md); the messages are
**identical**, so a page that shows the server's text for `400`, `409`, `503` and `504` needs no new table.

## Request

`POST /api/documents/{id}/summary`

```json
{ "preference": "auto" }
```

- The body is **optional**. No body, an empty object, `null` or an empty `preference` is **Auto**.
- `preference`: `"auto"`, `"local"` or `"cloud"`, in any letter case; anything else is `400`.
- There is no question, no history and no length: the summary is always one short paragraph (spec FR-005).

## Response `200 OK`

```json
{ "summary": "The agreement, signed in May, sets ...", "truncated": false, "backend": "local" }
```

- `summary`: one paragraph of about three to five sentences, plain text, in the language of the document.
- `truncated`: true only when the text was longer than the limit the model reads (`Qa:MaxDocumentCharacters`); the summary is then
  based on **the first part** only. The page says so.
- `backend`: `"local"` or `"cloud"`; always `"local"` with the delivered settings.

## Failure responses

Body is always `{ "error": "..." }`.

| Status | When | `error` |
|---|---|---|
| `400` | `preference` is not auto, local or cloud (checked first) | `The preference must be auto, local or cloud.` |
| `404` | no such document | `Document not found.` |
| `409` | the document is not ready | `The document is not ready. Current status: processing.` (or `failed`) |
| `409` | **new**: the trimmed text is under 50 characters | `The document has too little text to summarise.` |
| `409` | `preference` is `cloud` and the document is private | `This document is private, so it can only be answered on this computer. Choose Auto or Local.` |
| `503` | `preference` is `cloud` and the cloud is off or has no key | `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` |
| `503` | the model chosen cannot be reached | the provider's message (Ollama: `The answering model is not available. Check that Ollama is running and the model is installed.`; cloud: `The cloud model is not available. Check the cloud settings and your connection.`) |
| `504` | the model gave no answer in time | `The model did not answer in time. Please try again.` (cloud: `The cloud model did not answer in time. Please try again.`) |
| `500` | anything else | `The answer could not be produced.` (no detail) |

Rules that hold in every case:

- **Order of checks**: preference → document found → ready → enough text → routing → model. A `404` or a `409` therefore comes
  before any routing message, and **no model is called** when any of them refuses.
- **No fallback**: a summary routed to the cloud that fails is not retried locally.
- **A private document never reaches the cloud**, with any preference and any score, cloud on or off.
- **Nothing is stored**: the same request sent twice runs the model twice and may give two different texts.
- The document text is data, not instructions: an instruction inside it does not change what is asked of the model.
- With the delivered settings every `auto` and `local` request is answered by Ollama.
