# Quickstart: Checking the Signature Buttons

Fake files only, never a real document. The files are built by a small helper and by the tests, and live in
`~/Desktop/prove-scriptorium`. The checks with the user use a throwaway API on port 5099 and a separate page on 4299, never the
user's own 5034 and 4200, as in the earlier features. The cloud stays off and is never called.

## Prerequisites

- `dotnet build` and `dotnet test` pass at the start (the baseline numbers are written down in the validation results).
- Ollama is **not** needed for this feature; switching it off is part of scenario 4.
- If the user wants it, a test file signed with their own provider (a fake document, put in `~/Desktop/prove-scriptorium`) is the best
  proof for Word and Excel; only the list of its contents is looked at to see where the signature is stored.
- Fake files: a signed PDF (one signature, with a name and a date), a signed PDF with two signatures, an unsigned PDF, a signed
  Word with one signature, a signed Excel with two signatures, an unsigned Word, an unsigned Excel, a text file that quotes the
  wording of the note, and a signed Word with no text.

## Scenarios

1. **Signed Word and Excel are recognised at upload** (User Story 1): upload the signed Word and the signed Excel, open **Text**:
   the text starts with one line per signature (signer and date as declared), then a blank line, then the text. The unsigned
   Word and Excel give exactly the text they gave before.
2. **Signature author** (User Story 2): on each signed file the card lists the signers in the order of the file; an unknown signer
   reads `Unknown signer`.
3. **Signature date** (User Story 3): the dates are shown as declared, with the zone only when declared; with two signatures
   each date has its signer; a signature with no date says `No signing date declared in the file.`
4. **No model needed**: stop Ollama (or leave it off) and choose **Cloud** in **Answer with**: both buttons still answer, with no
   cloud message and no waiting.
5. **No signature** (User Story 4): the unsigned PDF, Word and Excel, the text file that quotes the wording, and a file uploaded
   before this feature all say `This document has no digital signature.` (to check the last one, keep a Word uploaded with the
   old code, or delete and upload an unsigned one).
6. **The page** (User Story 5): the buttons sit next to the other two and look alike; the cards appear above the conversation;
   **New conversation** and opening another document clear them; the buttons are disabled while a request runs; a deleted
   document and an unreachable server give clear messages; the note about the signature not being validated is there.
7. **Endpoint**: `GET /api/documents/{id}/signatures` returns the entries (see the contract), `404` for an unknown id, `409` for a
   document still processing, an empty list for a text file.
8. **A signed Word with no text** is accepted and shows its signature.
9. **Automated**: `dotnet test`: the reader, the decorator, the format round trip with the PDF reader, the service and the
   endpoint, all with fake files and no key.
10. **No regression, no secret, no spend**: the tests of features 001 to 008 pass unedited (`WordDocumentParserTests`,
    `ExcelDocumentParserTests`, `PdfSignatureNoteReaderTests`, `SignerNameReaderTests` in particular); `dotnet build` has no
    warning; `npm test` and `npm run build` pass; `git diff main --name-only` shows no `.csproj`, `package.json`, lock file or
    migration; `git grep` for keys finds only code that reads the key option.

## Expected outcome

Every scenario behaves as written, with no model call, no cloud call and no new package.
