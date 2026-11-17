# Validation Results: Signature Author and Signature Date Buttons

Baseline before any change (T001, branch `013-signature-buttons`): `dotnet build` 0 warnings; Core 535 and Infrastructure 317
tests passing; frontend 206 passing.

## Foundational: one format for the signature line (2026-11-18)

`SignatureNote` (Core) now defines the line and reads it back; `SignerNameText` holds the name cleaning that was in
`SignerNameReader`; `PdfSignatureNoteReader` builds its line with `SignatureNote`. Backend after this step: Core 571 and
Infrastructure 336 passing, 0 warnings. `PdfSignatureNoteReaderTests`, `SignerNameReaderTests` and `PdfDocumentParserTests` were
not edited and pass; a round-trip test (`SignatureNoteRoundTripTests`) shows that what the PDF reader writes is read back as written.

## User Story 1: signed Word and Excel files are recognised at upload (2026-11-18)

Throwaway API on port 5099 (cloud off with an empty key, temporary database and folder), page on 4299. Fake files only, with
self-made certificates of invented names (built by a throwaway program kept outside the project).

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. One signature, name, date with a zone | `signed-one.docx` | text starts with `file firmato con firma digitale da Mario Rossi Prova — 2026-03-14 10:32:00 +01:00`, a blank line, then the text | Claude (stored text), User (Text popup) |
| 1. Two signatures, the second with no date | `signed-two.xlsx` | two lines in the order of the file, the second with no date | Claude, User |
| 1. Date only in the XAdES properties | `signed-xades-time.docx` | the date is taken from `SigningTime` | Claude |
| 1. No certificate | `signed-no-signer.xlsx` | `(firmatario sconosciuto)` with the declared date | Claude |
| 1. Chain of certificates (authority and signer) | `signed-chain.docx` | the signer's name, not the authority's | Claude |
| 8. Signed Word with no text | `signed-no-text.docx` | accepted and ready; the text is only the line | Claude, User |
| 1. Unsigned Word and Excel | `unsigned.docx`, `unsigned.xlsx` | the text they gave before, no line | Claude, User |

All files were uploaded not private (`isPrivate` read back as false in the list). The user reported all fine.

Automated tests at the end of User Story 1: backend 571 (Core) + 386 (Infrastructure) passing, 0 build warnings. No existing test was
edited (`WordDocumentParserTests` and `ExcelDocumentParserTests` among them).

Not yet tried: a file signed by a real certification provider (none was available), only the fake files above.

## User Story 2: Signature author (2026-11-18)

Throwaway API on port 5099 (cloud off, empty key; for the check the model server address pointed nowhere, so Summarise answered
"The answering model is not available" while the signature button still worked), page on 4299. Fake files only.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 2. One signer | `signed-one.docx` | `Mario Rossi Prova` | Claude (endpoint), User (page) |
| 2. Two signers in order | `signed-two.xlsx` | `Anna Verdi Prova`, `Luca Bianchi Prova` | Claude, User |
| 2. No certificate | `signed-no-signer.xlsx` | `Unknown signer` | Claude, User |
| 2. Chain of certificates | `signed-chain.docx` | `Sara Blu Prova` (not the authority) | Claude, User |
| 2. Signed PDFs without a name | `signed-one.pdf`, `signed-two.pdf` | `Unknown signer` each | Claude, User |
| 4. No model, Cloud chosen | any signed file | the card appears; no cloud message; the model address was unreachable | User |
| 7. Endpoint | known id / unknown id / failed document | `200` with entries / `404` `Document not found.` / `409` not ready (automated) | Claude |
| Time of the call | 13 documents | 2 to 33 milliseconds | Claude |
| Page | question box | the user found the question box too small once the buttons were added: it is now full width, 4 rows tall, with the selector and buttons wrapping below it. The user reported all fine | User |

Automated tests at the end of User Story 2: backend 595 (Core) + 393 (Infrastructure) passing, 0 build warnings; frontend 216
passing (baseline 206), `npm run build` with no warning. No existing test was edited; the only change to an existing spec set-up is
`'getSignatures'` in the API spy of the ask panel.

## User Story 3: Signature date (2026-11-18)

Throwaway API on port 5099 (cloud off, empty key), page on 4299. Fake files only.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 3. One dated signature, with a zone | `signed-one.docx` | `2026-03-14 10:32:00 +01:00` alone, as declared | User |
| 3. Two signatures, one with no date | `signed-two.xlsx` | `Anna Verdi Prova: 2026-03-14 09:00:00 +00:00` and `Luca Bianchi Prova: No signing date declared in the file.` | User |
| 3. Date only in the XAdES properties | `signed-xades-time.docx` | `2026-04-02 15:45:10 +02:00` | User |
| 3. Two PDF signatures, no names | `signed-two.pdf` | `Unknown signer: ...` with each zone (+01:00 and +00:00) | User |
| 3. No date | `signed-no-date.pdf` | `No signing date declared in the file.` | User |
| 6. Both cards together | any signed file | both cards stay on the page; each button replaces only its own | User |

The user reported all fine.

Automated tests at the end of User Story 3: backend unchanged (595 + 393 passing); frontend 226 passing (baseline 206), `npm run build`
with no warning.

## User Story 4: a clear answer when there is no signature (2026-11-18)

Throwaway API on port 5099 (cloud off, empty key), page on 4299. Fake files only.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 5. Unsigned PDF | `unsigned-one.pdf` | `This document has no digital signature.` from both buttons, no name, no date, no note, no alert | User |
| 5. Unsigned Word and Excel | `unsigned.docx`, `unsigned.xlsx` | the same message from both buttons | User |
| 5. A text file that quotes the wording | `quoted-wording.txt` | the same message: it is not taken for a signature | User |
| 5. A document with no signature lines in its text (what an earlier upload looks like) | `unsigned.docx` | the same message | User |

The user reported all fine.

Automated tests at the end of User Story 4: backend 595 (Core) + 399 (Infrastructure) passing, 0 build warnings; frontend 229
passing (baseline 206), `npm run build` with no warning. The six backend tests of this story passed at once (the behaviour came with
User Story 2) and pin it; the three frontend tests failed first and were then satisfied by the message in the two cards.

## User Story 5: the buttons fit the panel (2026-11-18)

Throwaway API on port 5099, page on 4299 (cloud off, empty key). The four action buttons (Summarise, Extract entities, Signature author,
Signature date) were checked on the page, with the question box made full width and four rows tall and the buttons wrapping below it.
The user asked whether the two buttons and their result were clear without any explanation and answered that everything is clear.

The cases of a deleted document and of an unreachable server were covered by the automated tests (the user did not report them
one by one on the page).

Automated tests at the end of User Story 5: backend 595 (Core) + 399 (Infrastructure) passing, 0 build warnings; frontend 247
passing (baseline 206), `npm run build` with no warning. The 18 tests of this story passed at once: the behaviour comes from the
helper shared with Summarise and Extract entities, and they pin it.

## Final run and summary of scenarios 1 to 10 (2026-11-18)

| Scenarios | Outcome |
|---|---|
| 1, 8 (signed Word and Excel at upload, signed Word with no text) | Seen on the stored text and in the Text popup, confirmed by the user (User Story 1) |
| 2, 4, 7 (Signature author, no model needed, endpoint) | Checked on the page and with the endpoint; the model address was made unreachable on purpose and the buttons still worked (User Story 2) |
| 3 (Signature date) | Checked on the page: zone as declared, several signatures each with its signer, no date, XAdES-only date (User Story 3) |
| 5 (no digital signature) | Checked on the page for an unsigned PDF, Word and Excel, a text file quoting the wording and a text with no lines (User Story 4) |
| 6 (the panel) | Checked on the page, with the question box enlarged at the user's request (User Story 5) |
| 9 (automated) | `OfficeSignatureNoteReaderTests`, `SignedOfficeDocumentParserTests`, `SignatureNoteTests`, `SignatureNoteRoundTripTests`, `DocumentSignatureServiceTests`, `SignatureEndpointTests` and the panel specs, all passing with fake files and no key |
| 10 (no regression, no secret, no spend) | See below |

Final automated run: `dotnet build` 0 warnings and 0 errors; `dotnet test` Core 595 and Infrastructure 399 passing (baseline 535 and
317); frontend 247 passing (baseline 206); `npm run build` with no warning.

- No existing test was edited. The only change to an existing test file is **additions** to `ReadyDocumentTextTests.cs` (two new
  tests, 23 lines added, none removed); the only change to an existing spec set-up is `'getSignatures'` in the API spy of the ask panel.
  `WordDocumentParserTests`, `ExcelDocumentParserTests`, `PdfSignatureNoteReaderTests`, `SignerNameReaderTests` and
  `PdfDocumentParserTests` pass untouched.
- No `.csproj`, `package.json` or `package-lock.json` changed, no migration and no entity was added; `WordDocumentParser`,
  `ExcelDocumentParser`, `AiRequest`, `IAIProvider`, `ICloudAIProvider`, both providers and `ModelRouter` are untouched.
- No model, router, scorer or provider type is used by `DocumentSignatureService`, `SignatureEndpoints`, `SignatureNote` or the Office
  reader; no `HttpClient`, no `.Result`, `.Wait`, `dynamic`, reflection or `!` in the new code; the longest new method is 22 lines.
- `git grep` for keys finds only code that reads the key option.

**Said plainly:** no real signed file of a certification provider has been tried yet; only fake files with self-made certificates of
invented names. The real cloud service was never called and no model was called by these buttons. The user's own provider will be
tried later on a signed Excel they will supply: the first thing to look at is where the signature is stored in the file (a
`_xmlsignatures` folder is what this feature reads).
