# API Contract: Extracting the entities of a document

New endpoint. Everything about preferences, backends and refusals is the same as for questions and summaries, see
[../../005-model-router/contracts/ask-routing-api.md](../../005-model-router/contracts/ask-routing-api.md) and
[../../006-summaries/contracts/summary-api.md](../../006-summaries/contracts/summary-api.md); the messages are **identical**, so a
page that shows the server's text for `400`, `409`, `503` and `504` needs no new table. One message is new: the `409` for a text
that is too short.

## Request

`POST /api/documents/{id}/entities`

```json
{ "preference": "auto" }
```

- The body is **optional**. No body, an empty object, `null` or an empty `preference` is **Auto**.
- `preference`: `"auto"`, `"local"` or `"cloud"`, in any letter case; anything else is `400`.
- There is no question, no history and no list of types: the five types are fixed (spec FR-002).

## Response `200 OK`

```json
{
  "groups": [
    { "type": "people",        "entities": ["Mario Rossi", "Anna Bianchi"] },
    { "type": "organizations", "entities": ["Alfa Srl", "Beta Spa"] },
    { "type": "dates",         "entities": ["12 March 2026", "14 March 2026"] },
    { "type": "amounts",       "entities": ["15,000 euro"] },
    { "type": "places",        "entities": ["Turin", "Milan", "Bologna"] }
  ],
  "truncated": false,
  "backend": "local"
}
```

- `groups`: only the types that have at least one entity, always in the order people, organizations, dates, amounts, places. It is
  **empty** (`"groups": []`) when nothing was found; that is a success, not an error.
- `entities`: no repeats; each one written as in the document; in the order of its first appearance in the text read. Every one
  of them appears in the part of the document that was read (spec FR-003).
- `truncated`: true only when the text was longer than the limit the model reads (`Qa:MaxDocumentCharacters`); the entities are
  then based on **the first part** only. The page says so.
- `backend`: `"local"` or `"cloud"`; always `"local"` with the delivered settings.

## Failure responses

Body is always `{ "error": "..." }`.

| Status | When | `error` |
|---|---|---|
| `400` | `preference` is not auto, local or cloud (checked first) | `The preference must be auto, local or cloud.` |
| `404` | no such document | `Document not found.` |
| `409` | the document is not ready | `The document is not ready. Current status: processing.` (or `failed`) |
| `409` | **new text**: the trimmed text is under 50 characters | `The document has too little text to extract entities from.` |
| `409` | `preference` is `cloud` and the document is private | `This document is private, so it can only be answered on this computer. Choose Auto or Local.` |
| `503` | `preference` is `cloud` and the cloud is off or has no key | `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` |
| `503` | the model chosen cannot be reached | the provider's message (Ollama: `The answering model is not available. Check that Ollama is running and the model is installed.`; cloud: `The cloud model is not available. Check the cloud settings and your connection.`) |
| `504` | the model gave no answer in time | `The model did not answer in time. Please try again.` (cloud: `The cloud model did not answer in time. Please try again.`) |
| `500` | the reply of the model could not be read | `The entities could not be extracted.` (no detail) |
| `500` | anything else | `The answer could not be produced.` (no detail) |

Rules that hold in every case:

- **Order of checks**: preference → document found → ready → enough text → routing → model → readable reply. A `404` or a `409`
  therefore comes before any routing message, and **no model is called** when any of them refuses.
- **No fallback**: an extraction routed to the cloud that fails is not retried locally.
- **A private document never reaches the cloud**, with any preference and any score, cloud on or off.
- **Nothing is stored**: the same request sent twice runs the model twice and may give two different lists.
- **Nothing is invented**: an entity the model names that is not in the text that was read is dropped before the response.
- The document text is data, not instructions: an instruction inside it does not change what is asked of the model.
- With the delivered settings every `auto` and `local` request is answered by Ollama.
