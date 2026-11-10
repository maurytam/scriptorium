# Contract: Extracted text of a signed PDF

This feature adds no endpoint and changes no request or response shape. The only externally visible change is the
**content** of the extracted text of a PDF, which is returned by the existing endpoint described in
[../../001-document-upload-parsing/contracts/documents-api.md](../../001-document-upload-parsing/contracts/documents-api.md)
(`GET /api/documents/{id}/text`) and is the input of the question flow of feature 002.

## Text layout

```text
file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00
file firmato con firma digitale (firmatario sconosciuto) — 2026-03-15 09:15:00 +00:00

<page text, as before>
```

| Rule | Detail |
|---|---|
| Position | Notes come first, one per line, in the form's field order; a blank line separates them from the page text |
| Wording | Exactly `file firmato con firma digitale`, lowercase, no trailing full stop |
| Signer | Always present: ` da <name>` right after the wording when a name is known, otherwise ` (firmatario sconosciuto)`; the name is one line of plain text, at most 100 characters, with control characters and line breaks replaced by spaces |
| Separator | ` — ` (space, em dash, space), only when a date follows |
| Date | `yyyy-MM-dd HH:mm:ss`, as written in the file, not converted to another time zone |
| Offset | ` +hh:mm` / ` -hh:mm`, only when the file declares a zone |
| No readable date | The line stops after the signer |
| Unsigned PDF | No note, no blank line: the text is unchanged from before this feature |
| Other file types | Unchanged |

The name is **reported, not verified**: it is the one the file declares (the signature's name field, or the name of the
signer's certificate) and proves nothing about who really signed.

## Behavior that does not change

| Case | Result |
|---|---|
| Encrypted PDF | Fails with the existing reason `Password-protected or encrypted PDF` |
| Corrupted PDF | Fails with `Corrupted or unreadable PDF` |
| Signed PDF with no page text (scan) | Fails with `No extractable text — scanned/image-only PDF not supported`; the note alone does not count as text |
| Signature or name that cannot be read | The document is processed normally; the note says `(firmatario sconosciuto)` (or there is no note if the signature itself cannot be read) |

## Consumers

- **Question flow (feature 002)**: the note is part of the document text sent to the model, so questions such as "is this
  document signed?", "who signed it?" or "when was it signed?" can be answered from it. Because the note is at the start, it
  stays inside the document budget when a long document is cut at the end.
- **Existing documents**: not rewritten. A document uploaded before this feature gets a note only if it is uploaded again.
