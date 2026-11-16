# API Contract: Choosing the model for a question

Changes to `POST /api/documents/{id}/ask` of feature 002
([../../002-ask-document-questions/contracts/ask-api.md](../../002-ask-document-questions/contracts/ask-api.md)). Everything
not listed here is **unchanged**: the path, `question`, `history`, the limits, the `404`, the `409` for a document not ready, the
`500`. A client that sends nothing new keeps getting what it got before.

## Request: new optional field `preference`

```json
{
  "question": "Compare the penalties and explain why they differ?",
  "history": [],
  "preference": "auto"
}
```

- `preference` (optional): `"auto"`, `"local"` or `"cloud"`, in any letter case.
  - absent, `null` or empty: **Auto** (the server decides, FR-003).
  - `"local"`: always answered on this computer.
  - `"cloud"`: answered by the cloud model, if it can be.
  - any other value: `400`, see below. No model is called.
- The server keeps nothing: the preference is sent with every question.

## Response `200 OK`: new field `backend`

```json
{ "answer": "The penalties differ because ...", "truncated": false, "backend": "local" }
```

- `backend`: `"local"` (answered by the model on this computer) or `"cloud"` (answered by the cloud model). With the delivered
  settings it is always `"local"`.
- `answer` and `truncated` are as before.

## New failure responses

| Status | When | `error` |
|---|---|---|
| `400` | `preference` is not auto, local or cloud | `The preference must be auto, local or cloud.` |
| `409` | `preference` is `cloud` and the document is **private** (checked before anything else is asked of a model) | `This document is private, so it can only be answered on this computer. Choose Auto or Local.` |
| `503` | `preference` is `cloud` and the cloud is **switched off**, or switched on **without a key** | `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.` |
| `503` | the cloud was chosen (by the person, or by Auto) and the service **cannot be reached** | `The cloud model is not available. Check the cloud settings and your connection.` |
| `504` | the cloud was chosen and gave **no answer in time** | `The cloud model did not answer in time. Please try again.` |

Rules that hold in every case:

- **No fallback.** A question that was routed to the cloud and failed is **not** retried on the local model.
- **A private document never reaches the cloud**, with any preference and any complexity, whether the cloud is on or off.
- With the **delivered settings** (cloud off) a request with `auto` or `local` behaves exactly as before this feature; only
  `backend: "local"` is new in the answer.
- The order of checks is: question valid → document found → document ready → routing → model. A `404` or a `409` "not ready"
  therefore comes before any routing message, and no model is called when routing refuses.
- Bodies keep the existing shape `{ "error": "..." }`; the web page shows the text of `400`, `409`, `503` and `504` as it is
  (existing behaviour), so it needs no new message table.
