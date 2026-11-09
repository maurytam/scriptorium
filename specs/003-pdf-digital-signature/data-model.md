# Data Model: Digital Signature Note for Signed PDFs

No entity, table or column is added or changed. There is no migration. The only data produced by this feature is a line
of text inside the existing `ExtractedText` of a document (feature 001).

## Digital signature note

A transient value built while a PDF is parsed; it is never stored on its own.

| Field | Meaning | Rules |
|---|---|---|
| Wording | The fixed text `file firmato con firma digitale` | Always present, never translated (spec Assumptions) |
| Signer name | Who signed, as the file declares it | Always present once User Story 3 is built: from the signature's `/Name` if not blank, otherwise from the signer's certificate, otherwise the literal `firmatario sconosciuto`; never guessed (FR-011 to FR-013, FR-016). One line, at most 100 characters (FR-014). Introduced by `da` when a name is known; the unknown signer is written in parentheses |
| Signing date and time | What the signature declares about itself | Optional. Present only if the signature has a readable date with at least date, hour and minute (research R3). Introduced by ` — ` |
| Offset | The time zone of the declared date | Optional. Shown only if the raw date carried a zone designator (`Z`, `+hh'mm'`, `-hh'mm'`) |

### Rendered line

```text
file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00   name and date with zone
file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00          name, date without zone
file firmato con firma digitale da Mario Rossi                                name, no readable date
file firmato con firma digitale (firmatario sconosciuto) — 2026-03-14 10:32:00 +01:00   unknown signer, date with zone
file firmato con firma digitale (firmatario sconosciuto)                                unknown signer, no readable date
```

### Where the signer name comes from

| Situation in the signature | Name shown |
|---|---|
| `/Name` present and not blank | That text (decoded; cleaned) |
| `/Name` missing or blank, `/Contents` holds a certificate that the signer-info points to | The common name of that certificate (cleaned) |
| Both, with different values | `/Name` |
| Certificates present but none matches the signer-info, or the structure cannot be read | `firmatario sconosciuto` |
| Neither | `firmatario sconosciuto` |

### Relationship to the extracted text

For a PDF with *n* signatures (n ≥ 0) the extracted text is:

```text
<note 1>
...
<note n>
<blank line>            (only when n ≥ 1)
<page text>
```

For n = 0 the text is the page text alone, byte for byte what the parser produced before this feature (FR-006).

### Which signatures count

| Situation in the file | Produces a note? |
|---|---|
| Signature field whose `/V` points to a signature dictionary | Yes, one note |
| Signature field with no `/V` (empty placeholder) | No (FR-007) |
| No form, or a form with no signature field | No |
| Several signed fields | One note each, in the form's field order (FR-003) |
| Malformed form or signature that cannot be read | No note, and the upload goes on (FR-010) |
| Signature whose name or certificate cannot be read | The note, with `firmatario sconosciuto` (FR-013, FR-015) |

### State transitions

None. The note is computed each time a PDF is parsed, at upload time; documents already stored are not changed.
