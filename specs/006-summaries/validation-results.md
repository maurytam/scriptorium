# Validation results: Summaries

Manual checks of [quickstart.md](./quickstart.md) on branch `008-summaries`. Files: fake documents in
`~/Desktop/prove-scriptorium`; no real document is used. No cloud key exists anywhere and the real cloud service is never called.

How the results were obtained:

- **Endpoint**: checked by Claude with `curl` on a throwaway API (port 5099, database and files in the scratchpad, stopped
  afterwards), with the real local model (Ollama, `qwen3.5:4b`).
- **Page in the browser**: tried by the user on a separate Angular dev server (port 4299, proxied to the throwaway API), so the
  user's own API (5034) and page (4200) were not touched. The user reported "tutto ok"; the exact observations were not recorded
  one by one.

## User Story 1: a ready document is summarised on this computer (2026-11-17)

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. A normal document is summarised locally | `facts.txt` | `200`, `"backend":"local"`, `"truncated":false`, one paragraph that matches the file (about 31 s the first time) | Claude (endpoint), User (page) |
| 2. A private document is summarised locally | `router-private.txt` (**really** private, see the correction below), no body and `{"preference":"local"}` | `200`, `"backend":"local"`, a paragraph about the file (checked again on 2026-11-17 during User Story 2) | Claude (endpoint) |
| 4. Too little text | `tiny.txt` | `409` `The document has too little text to summarise.` | Claude (endpoint), User (page) |
| 4. A document that is not ready | `broken.docx` (ends up *failed*) | `409` `The document is not ready. Current status: failed.` | Claude (endpoint), User (page) |
| 4. An unknown id | random id | `404` `Document not found.` | Claude (endpoint) |
| 7. Nothing is kept | `facts.txt`, summarised twice | Two requests reached the model (about 16 s each); the document list, the extracted text, the files on disk and the database dump are identical before and after | Claude |
| 6. The page (button, progress, card, label, reset, **Send**) | fake files | As expected | User |

Not looked at: the Ollama console for the refused cases (the answers have the expected shape and the model is not called by the
code, which the automated tests check with strict mocks).

Notes:

- The small local model sometimes gets a detail wrong (it wrote "January 31, 2026 and 2027" where the file says 31 January 2027,
  and merged two signatures in one sentence). It is the behaviour of a 4B model, not something the code can fix; the prompt
  already says to use only the document.
- A first attempt on the user's own page (4200) showed *This document no longer exists* after **Summarise**: that API (5034) had
  been started before the new code and has no `/summary` route, so it answered `404` with an empty body, which the page words as
  "document gone" (the existing behaviour for every `404`). Not a defect of this feature; the checks were repeated on the 4299
  page.
- **Correction:** the first run of scenario 2 did **not** test a private document. The throwaway database got `router-private.txt`
  through a `curl` upload whose `isPrivate` field never reached the API (a mistake in Claude's shell command), so the file was
  stored as not private. It was noticed in User Story 2, when Cloud on that file gave `503` instead of the private `409`. The file
  was uploaded again with `isPrivate=true` (listed as private), and scenario 2 was repeated: `200`, `"backend":"local"` with no
  body and with `{"preference":"local"}`. The page check of User Story 1 used `facts.txt`-like documents, not a private one.
- While checking, the **New conversation** button was changed to appear also when only a summary is shown (the user's choice).

Automated tests at the end of User Story 1: backend 318 (Core) + 263 (Infrastructure) passing, 0 build warnings (baseline 269 +
233); frontend 163 passing (baseline 143), `npm run build` with no warning.

## User Story 2: the summary follows the same model rules as the questions (2026-11-17)

Throwaway API on port 5099 (cloud off with no key; then `Routing__CloudEnabled=true` with an empty key), page on 4299.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 3. Cloud chosen while it is off | `facts.txt` | `503` `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.`; on the page the same text in the alert, no card | Claude (endpoint), User (page) |
| 3. Cloud chosen for a private document | `router-private.txt` (uploaded with `isPrivate=true`, checked in the list) | `409` `This document is private, so it can only be answered on this computer. Choose Auto or Local.`; same on the page | Claude (endpoint), User (page) |
| 3. A preference that means nothing | `facts.txt`, and a random id | `400` `The preference must be auto, local or cloud.` in both cases (checked before the `404`) | Claude (endpoint) |
| 3. Cloud and too little text | `tiny.txt` | `409` `The document has too little text to summarise.` (the document checks come before routing) | Claude (endpoint) |
| 2. Auto and Local on a private document | `router-private.txt` | `200`, `"backend":"local"` with no body and with `{"preference":"local"}` | Claude (endpoint) |
| 8. Cloud switched on without a key | `facts.txt` with Cloud; `long.txt` with Auto | Cloud: the same `503`; Auto: `200`, `"backend":"local"`, `"truncated":true` (no notice on the page yet, User Story 3) | Claude (endpoint) |
| Page: the selector | fake files | Cloud is selectable; the refusals show as they are; the selector goes back to Auto when another document is opened, when the panel is closed and on **New conversation**. The user reported all fine | User |

No key was set anywhere and the real cloud service was never called (the key option was passed empty on purpose).

Automated tests at the end of User Story 2: backend 369 (Core) + 282 (Infrastructure) passing, 0 build warnings; frontend 168 passing,
`npm run build` with no warning. `DocumentSummaryService` needed no change for User Story 2: the only production change is that the
endpoint reads the preference of the request.

## User Story 3: a long document says what the summary covers (2026-11-17)

Throwaway API on port 5099 (cloud off, empty key), page on 4299.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 5. A long document | `long.txt` (63,910 characters; the model reads the first 8,000) | `200`, `"truncated":true`, `"backend":"local"`; on the page the card shows the note *This summary is based only on the first part of the document, which is too long to be read in full.* | Claude (endpoint), User (page) |
| 5. A short document | `facts.txt` | `200`, `"truncated":false`; no note on the page | Claude (endpoint), User (page) |
| 6. The note part of the page | fake files | No note after a failed request; the note goes away with the summary when another document is opened. The user reported all fine | User |

Automated tests at the end of User Story 3: backend 373 (Core) + 284 (Infrastructure) passing, 0 build warnings; frontend 172 passing,
`npm run build` with no warning. The cut of the text existed since User Story 1; this story only adds the note to the card.

## Final run, scenarios 9 and 10, and what this does not prove (2026-11-17)

| Scenario | Result |
|---|---|
| 9. The score of a summary and the stand-in cloud (automated) | Passes: `DocumentSummaryServiceTests` (with the default threshold Auto keeps even a long document local; with the threshold at 0 a long one goes to the stand-in and a short one does not; a private document and Local never reach it; a failing stand-in gives `ModelUnavailable` or `Timeout` and the local model is never called; the whole table of 36 combinations) and `SummaryEndpointTests` (the same through HTTP). No test needs a key or the network |
| 10. No regression, no secret, no spend | `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 373 (Core) + 284 (Infrastructure) passing. Frontend: 172 passing, `npm run build` with no warning. `git grep` for `sk-ant-` and `ApiKey` outside `specs`, `docs` and the Markdown files finds only the code that reads the key option and tests that set it to empty or null: no key value anywhere. `git diff main --name-only` shows no `.csproj`, `package.json`, `package-lock.json` or migration |

Comparison with the baseline of T001 (before any change):

| | Baseline | Now |
|---|---|---|
| Core tests | 269 | 373 |
| Infrastructure tests | 233 | 284 |
| Frontend tests | 143 | 172 |
| Build warnings | 0 | 0 |

No test was removed. In the existing frontend specs only two import lines and the list of methods of the API spy (`'summarize'`
added) changed; no existing backend test file was edited. The question flow was refactored (its loader, failure mapping, document
fence, preference parsing and status mapping moved to shared helpers) and `DocumentQuestionService` went from 145 to 82 lines
with the same public shape.

Review against the constitution (T052): every new method is under 25 lines; no `!` suppression, `.Result`, `.Wait()`, `dynamic` or
reflection was added; `ModelRouter` is still the only place that chooses between local and cloud; no `HttpClient` outside the two
providers; no Ollama or cloud code in the new classes. No deviation found.

What this does **not** prove:

- The real cloud service was **never called** and no cloud key exists anywhere. Every cloud-on case used a stand-in provider or the
  real provider with an empty key (which refuses).
- The **quality of the summaries** was judged by reading a few of them from the local 4B model, not measured. They follow the
  document, but a small model can get a detail wrong (a date, a merged sentence), and the length is asked for in sentences, not
  enforced.
