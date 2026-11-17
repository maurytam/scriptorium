# Data Model: Signature Author and Signature Date Buttons

No entity, table or column is added or changed. There is no migration. Nothing is stored: the signatures are read each time from
the extracted text that already exists (feature 001), where feature 003 wrote them for PDFs and this feature writes them for
Word and Excel.

## Signature line (the existing text format)

One line per signature at the start of the extracted text, then a blank line, then the document's own text (or nothing, when a
signed Word or Excel file has no text).

```text
file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00
file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00
file firmato con firma digitale da Mario Rossi
file firmato con firma digitale (firmatario sconosciuto) — 2026-03-14 10:32:00 +01:00
file firmato con firma digitale (firmatario sconosciuto)
```

The wording, `da`, the parentheses of the unknown signer, ` — ` and the date layout are defined once, in `SignatureNote` (Core).

## Signature entry (a value, never stored)

| Field | Meaning | Rules |
|---|---|---|
| Signer | Who signed, as the file declares it | `null` for the unknown signer; otherwise the name, one line, at most 100 characters |
| Signed at | The signing date and time, as the line shows it | `null` when the line has no date; otherwise `yyyy-MM-dd HH:mm:ss`, plus ` ±hh:mm` only when the file declared a zone |

## Signature result (a value, never stored)

| Field | Meaning |
|---|---|
| Signatures | The entries, in the order of the file; empty when the document has none (that is a success, not an error) |
| Failure kind and error | Only when the document is not found, not ready or its text is missing: the existing kinds `NotFound`, `NotReady`, `Internal` |

### Which documents are looked at

| Document type | Read? |
|---|---|
| `pdf`, `docx`, `xlsx` | Yes: the leading block of its stored text |
| `txt` | Never: always no signature |

### The leading block

| Situation in the stored text | Signatures |
|---|---|
| Lines from the first line that match the format, then a blank line or the end of the text | One entry per line, in order |
| The same wording further down, or after any other line | None |
| The first line is not in the format | None |
| A matching line with a name that contains ` — ` | The name is read whole; the date is the declared date at the end of the line |

## Where Word and Excel signatures are written

At upload, `SignedOfficeDocumentParser` puts one line per signature in front of the text the existing parser produced:

| Situation in the file | Result |
|---|---|
| No signature part, or none readable | The text of the existing parser, unchanged |
| One or more signature parts | A line each, in the order the library lists them, then a blank line, then the text (only the lines if the text is empty) |
| A signature with several certificates (a provider's chain) | The name of the signer's own certificate: not a certificate authority and not the issuer of another one of the signature; the first certificate if none passes |
| A signature with no readable certificate | A line with `(firmatario sconosciuto)` |
| A signature with no time, or one that is not an ISO date and time with hour and minute | A line with no date |
| Anything that cannot be read | No line; the text is as before; the upload goes on |

### State transitions

None. A document uploaded before this feature has no lines (Word and Excel) and shows as having no signature; deleting it and
uploading it again writes them (uploading the same content again without deleting is refused as a duplicate, as today).
