# Validation results: Extracted-Text Popup

Manual checks of [quickstart.md](./quickstart.md) on branch `005-extracted-text-popup`. Files: fake documents in
`~/Desktop/prove-scriptorium`; no real document is used.

How the results were obtained:

- **Endpoint** (scenario 11, in part): checked by Claude with `curl`-style requests on a throwaway API (port 5099, database in the
  scratchpad, stopped afterwards).
- **Popup in the browser** (scenarios 1 to 5 and 9): tried by the user with the real API and the Angular app. The user reported that
  every expected result was obtained; the exact observations were not recorded one by one.

## User Story 1: read the extracted text in a popup (2026-11-10)

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. Short text in the popup | `text-short.txt` | Title with the file name, the whole text with its line breaks, no cutting notice | User |
| 2. Closing: button, Escape, click outside; focus back on the Text button; keyboard-only pass | `text-short.txt` | As expected | User |
| 3. Signed PDF | `signed-with-name.pdf` | Text starts with the signature note `... da Mario Rossi — 2026-03-14 10:32:00 +01:00` | Claude (endpoint), User (popup) |
| 4. Only ready documents have the button | processing and failed rows | No Text button; present on ready rows | User |
| 5. Read-only and plain text | `text-markup.txt` | `<b>bold</b> <script>alert(1)</script>` shown literally, no bold, no alert; not editable; selectable and copyable | User |
| 9. Nothing changes | several documents | Documents, texts and the question panel unchanged; Ask and Delete work as before | User |
| 11. Endpoint (part) | `text-short.txt`, `text-markup.txt`, `signed-with-name.pdf`, `signed-scan.pdf`, unknown id | `200` with `truncated: false` and the right `totalCharacters`; `409` with `Document is not ready. Current status: failed.`; `404` | Claude |

Automated tests at the end of User Story 1: backend 117 (Core) + 187 (Infrastructure) passing, 0 build warnings; frontend 129 passing
(Karma, headless Chrome); `npm run build` without warnings; no `.csproj`, `package.json` or `package-lock.json` changed.

Not yet covered: scenarios 6, 7 and 8 (long texts and failure while loading, User Story 2) and the final run (scenario 10).
