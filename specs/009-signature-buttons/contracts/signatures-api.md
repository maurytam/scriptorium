# Contract: Signatures of a Document

One new read-only endpoint. No other endpoint changes.

## `GET /api/documents/{id}/signatures`

Returns the digital signatures recorded for a ready document, as the file declared them. No model is called; no body; no
preference.

### Success: `200 OK`

```json
{
  "signatures": [
    { "signer": "Mario Rossi", "signedAt": "2026-03-14 10:32:00 +01:00" },
    { "signer": null, "signedAt": null }
  ]
}
```

| Field | Meaning |
|---|---|
| `signatures` | One item per signature, in the order of the file. **Empty** when the document has no digital signature (not an error) |
| `signatures[].signer` | The signer's name, or `null` for an unknown signer |
| `signatures[].signedAt` | `yyyy-MM-dd HH:mm:ss`, plus ` ±hh:mm` only if the file declared a zone; `null` when no date is declared |

### Failures

The same bodies and statuses as the other document endpoints: `{ "error": "<message>" }`.

| Status | When | Message |
|---|---|---|
| `404` | The document does not exist | `Document not found.` |
| `409` | The document is not ready | `The document is not ready. Current status: <status>.` |
| `500` | The text of a ready document is missing | `The text of the document is not available.` |

There is no `400`, `503` or `504`: nothing is asked of a model.

### Rules

- Only `pdf`, `docx` and `xlsx` documents can have signatures; any other type gives `200` with an empty list.
- The whole stored text is read, not the part a model would read.
- The values are what the file declares; the signature is not validated.

## What the page shows (not part of the API)

| Button | Shows |
|---|---|
| **Signature author** | one line per signature with the signer (`Unknown signer` when `null`) |
| **Signature date** | one line per signature with the date, and the signer when there are several (`No signing date declared in the file.` when `null`) |
| either, empty list | `This document has no digital signature.` |
| either, with signatures | a note: `The name and date are what the file declares. The signature has not been validated.` |
