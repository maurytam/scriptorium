# Validation results: Digital Signature Note for Signed PDFs

Manual checks of [quickstart.md](./quickstart.md), run on 2026-11-10 on branch `004-pdf-digital-signature` after the
implementation of User Stories 1, 3 and 2 (tasks T011, T023 and T029). Files: fake PDFs in `~/Desktop/prove-scriptorium`, produced by
a throwaway generator outside the repository; fake names only, no real document. Model: Ollama `qwen3.5:4b`.

How the results were obtained:

- **Text of the documents** (scenarios 1, 2, 9, 10, 11): read by Claude from `GET /api/documents/{id}/text` on a throwaway
  API (port 5099, database in the scratchpad), then the files were uploaded by the user in the browser.
- **Edge cases without a model** (scenarios 3 and 4): read by Claude on the same kind of throwaway API, then uploaded by the
  user in the browser, who reported that every check passed.
- **Questions to the model** (scenarios 5, 6, 7, 12): asked by the user in the browser, each question 3 times in a row, a new
  conversation each time. The user reported that every expected result was obtained; the exact wording of each answer was
  not recorded.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. Signed PDF, note first | `signed-one.pdf` | Text starts with `file firmato con firma digitale (firmatario sconosciuto) — 2026-03-14 10:32:00 +01:00`, then a blank line | Claude |
| 2. Unsigned PDF unchanged | `unsigned-one.pdf` | No note, no leading blank line | Claude |
| 5. Is it signed? (English and Italian) | `signed-one.pdf` | Says it is signed, as expected | User |
| 6. When was it signed? | `signed-one.pdf` | Gives the date and time of the note, as expected | User |
| 7. Unsigned document | `unsigned-one.pdf` | Does not claim it is signed, as expected | User |
| 9. Signer from the name field | `signed-with-name.pdf` | `... da Mario Rossi — 2026-03-14 10:32:00 +01:00` | Claude |
| 10. Signer from the certificate only | `signed-with-certificate.pdf` | `... da ROSSI MARIO — ...` (not the authority's name) | Claude |
| 11. Name and certificate | `signed-name-and-certificate.pdf` | `da Mario Rossi` (the name field wins) | Claude |
| 11. Certificate not matching | `signed-certificate-unmatched.pdf` | `(firmatario sconosciuto)`, upload succeeds | Claude |
| 11. No name, no certificate | `signed-no-signer-info.pdf` | `(firmatario sconosciuto)` | Claude |
| 11. Blank name plus certificate | `signed-name-blank.pdf` | `da ROSSI MARIO` | Claude |
| 11. Name with a line break | `signed-name-injection.pdf` | One single line, name cut at 100 characters, the extra text not on its own line | Claude |
| 12. Who signed it? (English and Italian) | `signed-with-name.pdf` | Gives the name, as expected | User |
| 12. Unknown signer | `signed-no-signer-info.pdf` | Says the signer is unknown, as expected | User |
| 12. Unsigned PDF | `unsigned-one.pdf` | Does not invent a signer, as expected | User |
| 3. Two signatures | `signed-two.pdf` | Two note lines in file order (`2026-03-14 10:32:00 +01:00`, `2026-03-15 09:15:00 +00:00`), a blank line, the page text | Claude and user |
| 4. Signature without date | `signed-no-date.pdf` | One note with no date, upload succeeds | Claude and user |
| 4. Unreadable date | `signed-bad-date.pdf` | One note with no date, upload succeeds | Claude and user |
| 4. Empty signature field | `signature-placeholder.pdf` | No note | Claude and user |
| 4. Signed scan | `signed-scan.pdf` | Fails with `No extractable text — scanned/image-only PDF not supported` | Claude and user |
| 8. No regression | all automated tests | See below | Claude |

Scenario 4 also lists an encrypted or corrupted PDF: no manual file was used; both cases are covered by the existing
automated tests (`PdfDocumentParserTests`), which still pass.

## Final run (T032)

- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 96 (Core) + 182 (Infrastructure) passing, 0 failing. No test of the earlier features was removed
  (`git diff` against the commit before the feature shows no deleted test method), so SC-004 holds.
- `git diff` against the commit before the feature: no `.csproj`, no package reference changed (FR-009).
- Constitution review (T031): every class has one responsibility; the longest method has 23 lines (limit 30); no `!`, no
  `.Result`/`.Wait()`, no reflection; the parser and the readers depend on each other only through interfaces and constructors.
  No deviation found.

Scenarios 1 to 12 are all covered above.
