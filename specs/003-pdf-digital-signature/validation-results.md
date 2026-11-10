# Validation results: Digital Signature Note for Signed PDFs

Manual checks of [quickstart.md](./quickstart.md), run on 2026-11-10 on branch `004-pdf-digital-signature` after the
implementation of User Stories 1 and 3 (tasks T011 and T023). Files: fake PDFs in `~/Desktop/prove-scriptorium`, produced by
a throwaway generator outside the repository; fake names only, no real document. Model: Ollama `qwen3.5:4b`.

How the results were obtained:

- **Text of the documents** (scenarios 1, 2, 9, 10, 11): read by Claude from `GET /api/documents/{id}/text` on a throwaway
  API (port 5099, database in the scratchpad), then the files were uploaded by the user in the browser.
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

Automated tests at the end of User Story 3: 96 (Core) + 164 (Infrastructure) passing, 0 build warnings, no `.csproj`
changed.

Not yet covered: scenarios 3 and 4 (several or incomplete signatures, User Story 2) and scenario 8 (no regression, task T032).
