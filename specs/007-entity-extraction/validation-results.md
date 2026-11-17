# Validation results: Entity Extraction

Manual checks of [quickstart.md](./quickstart.md) on branch `009-entity-extraction`. Files: fake documents in
`~/Desktop/prove-scriptorium`; no real document is used. No cloud key exists anywhere and the real cloud service is never called.

How the results were obtained:

- **Endpoint**: checked by Claude with `curl` on a throwaway API (port 5099, database and files in the scratchpad, cloud off and an
  empty key, stopped afterwards), with the real local model (Ollama, `qwen3.5:4b`). After uploading, the list of documents was read
  and the `isPrivate` flag of `router-private.txt` was checked (`True`).
- **Page in the browser**: tried by the user on a separate Angular dev server (port 4299, proxied to the throwaway API), so the
  user's own API (5034) and page (4200) were not touched. The user reported all fine; the exact observations were not recorded one
  by one.

## User Story 1: the key entities of a ready document (2026-11-17)

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. A normal document gives its entities, grouped | `facts.txt` | `200`, `"backend":"local"`, `"truncated":false`, five groups: people Mario Rossi and Anna Bianchi; organizations Alfa Srl and Beta Spa; dates 12 March 2026 and 14 March 2026; amount 15,000 euro; places Turin, Milan and Bologna. All 10 entities of the file, none missing, none added (about 24 s the first time) | Claude (endpoint), User (page) |
| 2. A private document is processed locally | `router-private.txt` (uploaded as private, flag checked in the list), no body and `{"preference":"local"}` | `200`, `"backend":"local"` both times; people Luca Verdi and Elena Neri, organizations Gamma Srl and Delta Spa, dates 5 May 2026, 30 September 2026 and 31 January 2027, amount 82,000 euro, place Genoa: all of the file | Claude (endpoint) |
| 4. Too little text | `tiny.txt` | `409` `The document has too little text to extract entities from.` | Claude (endpoint), User (page) |
| 4. A document that is not ready | `broken.docx` (ends up *failed*) | `409` `The document is not ready. Current status: failed.` | Claude (endpoint), User (page) |
| 4. An unknown id | random id | `404` `Document not found.` | Claude (endpoint) |
| 7. Nothing is kept | `facts.txt`, asked twice | Two requests reached the model (about 9 s each); the document list, the extracted text, the files on disk and the database dump are identical before and after | Claude |
| A long list | `many-names.txt` (made for the occasion: 20 invented people and 15 places) | 20 people and 15 places, every one written in the file | Claude (endpoint) |
| 6. The page (button, progress, card, headings only for the types found, label, reset, **Send** and **Summarise** still work, summary and entities together, a long list on a wide and a narrow window in both themes) | fake files | As expected. The user reported all fine | User |

Not looked at: the Ollama console for the refused cases (the answers have the expected shape and the model is not called by the
code, which the automated tests check with strict mocks).

Notes:

- On these three documents the 4B model copied every entity exactly as written, so the rule "an entity that is not in the text read
  is dropped" never had to act in practice. It is proved by the automated tests (`EntityVerifierTests`, `DocumentEntityServiceTests`,
  `EntityEndpointTests`) and stays as the guarantee for the cases where the model gets it wrong. A date the model rewrites would be
  dropped (plan, point 3).
- This was one run on three documents: it says the extraction works, not how often the model is right.

Automated tests at the end of User Story 1: backend 479 (Core) + 296 (Infrastructure) passing, 0 build warnings (baseline 373 +
284); frontend 193 passing (baseline 172), `npm run build` with no warning. No existing test was edited; the only change to an
existing spec is `'extractEntities'` in the list of methods of the API spy. `DocumentSummaryService` was changed to use
`RoutedDocument` and its tests passed unchanged.

## User Story 2: extraction follows the same model rules as questions and summaries (2026-11-17)

Throwaway API on port 5099 (cloud off with an empty key; then `Routing__CloudEnabled=true` with an empty key), page on 4299.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 3. Cloud chosen while it is off | `facts.txt` | `503` `The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer.`; on the page the same text in the alert, no card | Claude (endpoint), User (page) |
| 3. Cloud chosen for a private document | `router-private.txt` (uploaded as private; `private=True` read in the list before the check) | `409` `This document is private, so it can only be answered on this computer. Choose Auto or Local.`; same on the page | Claude (endpoint), User (page) |
| 3. A preference that means nothing | `facts.txt`, and a random id | `400` `The preference must be auto, local or cloud.` in both cases (checked before the `404`) | Claude (endpoint) |
| 3. Cloud and too little text | `tiny.txt` | `409` `The document has too little text to extract entities from.` (the document checks come before routing) | Claude (endpoint) |
| 2. Auto and Local on a private document | `router-private.txt` | `200`, `"backend":"local"` with `{"preference":"local"}` (and, in User Story 1, with no body) | Claude (endpoint) |
| 8. Cloud switched on without a key | `facts.txt` with Cloud; `facts.txt` and `long.txt` with Auto | Cloud: the same `503`; Auto: `200`, `"backend":"local"` (5 groups for `facts.txt`; `"groups":[]` and `"truncated":true` for `long.txt`, which says in every section that it holds no names, contracts or money) | Claude (endpoint) |
| Page: the selector | fake files | Cloud is selectable; the refusals show as they are with no card; the selector goes back to Auto when another document is opened, when the panel is closed and on **New conversation**; `long.txt` says no entities were found. The user reported all fine | User |

No key was set anywhere and the real cloud service was never called (the key option was passed empty on purpose).

Automated tests at the end of User Story 2: backend 530 (Core) + 315 (Infrastructure) passing, 0 build warnings; frontend 200 passing,
`npm run build` with no warning. `DocumentEntityService` needed no change for User Story 2 (it already routed with the preference it
was given, and the whole table of 36 combinations passes); the only production change is that the endpoint reads the preference of
the request.

## User Story 3: a long document says only the first part was read (2026-11-17)

Throwaway API on port 5099 (cloud off with an empty key), page on 4299.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 5. Long document | `long.txt` | `200`, `"truncated":true`, `"backend":"local"`, `"groups":[]` | Claude (endpoint) |
| 5. Document that fits | `facts.txt` | `200`, `"truncated":false`, 5 groups | Claude (endpoint) |
| 6. The notice on the page | `long.txt`, `facts.txt`, an error (Cloud), another document | `long.txt`: the amber notice "These entities are based only on the first part of the document, which is too long to be read in full." plus "No entities were found in this document.", also in the dark theme; no notice on `facts.txt` and on an error; the notice goes away with the card when another document is opened. The user reported all fine | User |

Automated tests at the end of User Story 3: backend 535 (Core) + 317 (Infrastructure) passing, 0 build warnings; frontend 205 passing,
`npm run build` with no warning.

## Final run and summary of scenarios 1 to 10 (2026-11-17)

| Scenarios | Outcome |
|---|---|
| 1, 4, 7 (entities of a document, nothing invented, page) | Seen with the real local model on fake files and confirmed on the page by the user (User Story 1) |
| 2, 3, 8 (model rules, refusals, cloud on without a key) | Checked on the endpoint and on the page (User Story 2) |
| 5, 6 (long document, notice) | Checked on the endpoint and on the page (User Story 3) |
| 9 (reply of the model, stand-in cloud) | Automated: `EntityReplyParserTests`, `EntityVerifierTests`, `DocumentEntityServiceTests`, `EntityEndpointTests`, all passing; no key or network needed |
| 10 (no regression, no secret, no spend) | See below |

Final automated run: `dotnet build` 0 warnings and 0 errors; `dotnet test` Core 535 and Infrastructure 317 passing (baseline 373 and
284, so only additions); frontend 205 passing (baseline 172) and `npm run build` with no warning.

- No existing test was edited: `DocumentSummaryServiceTests` and `SummaryEndpointTests` are untouched; the only changes to existing
  specs are `'extractEntities'` in the list of methods of the API spy and the new cases added at the end of the ask panel and API
  service specs.
- `git grep` for `sk-ant-` and `ApiKey` outside specs, docs and Markdown finds only the code that reads the key option and tests
  that use a fake key. No key value anywhere.
- No `.csproj`, `package.json` or `package-lock.json` changed, no migration and no entity was added, and `AiRequest`, `IAIProvider`,
  `ICloudAIProvider`, `OllamaProvider` and `ClaudeProvider` are untouched.
- `ModelRouter` is still the only place that chooses between local and cloud: the only other mentions of `ModelBackend` are the
  label written in the response DTOs and the result types. No `HttpClient` outside the two providers; no `.Result`, `.Wait`,
  `dynamic`, reflection or `!` in the new classes. No method of the new classes reaches 25 lines. `DocumentSummaryService` went from
  71 to 37 lines with the same public shape.

**The real cloud service was never called**: the key was empty or absent in every run, and the cloud-on cases use a stand-in
provider. **The quality of the entities was judged by reading them** on a few fake files; it is not a measure of how often the
model is right.
