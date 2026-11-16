# Validation results: Model Router

Manual checks of [quickstart.md](./quickstart.md) on branch `007-model-router`. Files: fake documents in
`~/Desktop/prove-scriptorium`; no real document is used. No cloud key exists anywhere and the real cloud service is never called.

How the results were obtained:

- **Endpoint** (scenarios 1 and 2, in part): checked by Claude with `curl` on a throwaway API (port 5099, database in the scratchpad,
  stopped afterwards), with the real local model.
- **Page in the browser**: tried by the user with the real API (port 5034, rebuilt with the new code), the Angular app and Ollama
  (`qwen3.5:4b`). The user reported "ok"; the exact observations were not recorded one by one.

## User Story 1: private documents and the default stay local (2026-11-16)

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 1. A normal document is answered as before | `facts.txt` | Answered by Ollama, `"backend":"local"` | Claude (endpoint), User (page) |
| 2. A private document is answered locally | `router-private.txt` (uploaded as private) | Answered by Ollama, `"backend":"local"` | Claude (endpoint), User (page) |

The page behaves as before (the "Answered on this computer" label arrives with User Story 2).

Automated tests at the end of User Story 1: backend 255 (Core) + 210 (Infrastructure) passing, 0 build warnings (baseline 117 + 187);
frontend untouched (131 passing).

## User Story 2: the person can choose Auto, Local or Cloud (2026-11-16)

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 3. Cloud chosen while it is off | `facts.txt` | The panel shows the "not available" message; Cloud is selectable; Auto afterwards answers locally with the label | User |
| 4. Cloud chosen for a private document | `router-private.txt` (private) | The panel shows the "private" message; no answer | User |
| 5. The selector starts from Auto | any | Back on Auto after closing and reopening | User |
| 6. Endpoint | `facts.txt`, `router-private.txt` | `local` and `auto`: `200`, `"backend":"local"`; `turbo`: `400` `The preference must be auto, local or cloud.`; `CLOUD`: `503` (normal) and `409` (private) with the contract messages | Claude |

The user reported "funziona"; the exact observations were not recorded one by one.

Automated tests at the end of User Story 2: backend 257 (Core) + 227 (Infrastructure) passing, 0 build warnings; frontend 143 passing
(Karma, headless Chrome); `npm run build` without warnings. Existing frontend specs got only mechanical changes: the new fourth `ask`
argument `'auto'` and `preference: 'auto'` in the expected body.

## User Story 3: the cloud can be switched on later without redesign (2026-11-16)

Scenario 7 was tried with the cloud switched on (`Routing__CloudEnabled=true`) and **no key**. No key exists anywhere and nothing was
sent anywhere. The API was a throwaway one started by Claude on port 5034 (the user had stopped their own), with a database and a
document folder in the scratchpad and only fake files (`facts.txt`, `long.txt`, `router-private.txt` private). The user's own database
and documents were not used.

| Scenario | File | Result | Seen by |
|---|---|---|---|
| 7a. Cloud chosen, cloud on, no key | `facts.txt` | `503` and the panel shows *The cloud model is not available. It is switched off or not set up…* | Claude (`curl`), User (page) |
| 7b. Cloud chosen on a private document | `router-private.txt` | `409` and the panel shows the "private" message | Claude (`curl`), User (page) |
| 7c. Auto, complex question (two `?`, "Compare", "Explain why", three earlier exchanges) on a long document | `long.txt` | `200`, `"backend":"local"`, answered by Ollama; the label says "Answered on this computer" | Claude (`curl`), User (page) |

The log of the throwaway API has no mention of the cloud service. The user confirmed that the three tries on the page behaved as expected.

Automated tests at the end of User Story 3: backend 269 (Core) + 233 (Infrastructure) passing, 0 build warnings. The stand-in cloud
(a mock `ICloudAIProvider`) proves the cloud-on cases; no test uses a key or the network. No production file changed in this phase.

## Final run: scenarios 8, 9 and 10, and the overview (2026-11-16)

| Scenario | Result |
|---|---|
| 1, 2 (User Story 1) | See above: normal and private documents answered locally, `"backend":"local"` |
| 3, 4, 5, 6 (User Story 2) | See above: Cloud while off gives `503`, Cloud on a private document gives `409`, the selector starts from Auto, the endpoint follows the contract |
| 7 (User Story 3) | See above: with the cloud on and no key, Cloud gives `503` and Auto with a complex question is answered locally |
| 8. Decision table and stand-in cloud | `ModelRouterTests` runs the whole table of `data-model.md` (private × preference × cloud off, on, on without a key × score below, at and above the threshold); `ComplexityScorerTests` covers the five signals, the fixed word list of 14 entries and the examples of the spec; the cloud-on cases use a stand-in `ICloudAIProvider` at service and endpoint level, including `503`/`504` with no fallback. No test uses a key or the network |
| 9. No regression | Backend: 269 (Core) + 233 (Infrastructure) passing, baseline 117 + 187. Frontend: 143 passing, baseline 131. `dotnet build` 0 warnings, 0 errors; `npm run build` without warnings. In the existing tests only the set-up line and the `AskAsync` calls (and, in the web specs, the new fourth `ask` argument `'auto'` and `preference: 'auto'` in the expected body) changed; no assertion was removed or loosened. `dotnet list package --include-transitive`: `Anthropic` 13.8.0 is the only new reference; `Microsoft.Extensions.AI` and `.Abstractions` 10.8.0 already came with `OllamaSharp` 5.5.0 (research R4). Only `Scriptorium.Infrastructure.csproj` changed among the project files; no `package.json` or `package-lock.json` changed |
| 10. No secret, no spend | `git grep -nIE 'sk-ant-\|ApiKey' -- ':!specs' ':!docs' ':!*.md'` finds only code that reads the key option, the tests that use no key or the fake `test-key-not-real`; no key value anywhere. `appsettings.json` has `"CloudEnabled": false` and no `ApiKey` (a test checks it) |

Review against the constitution (T045): `ModelRouter` is the only place that chooses between local and cloud (the only other mention of
`ModelBackend` is the DTO mapping it to the text `local` or `cloud`); `HttpClient` appears only in `OllamaProvider` and `ClaudeProvider`;
no Ollama and no cloud code in the same class; the scorer receives the question and two numbers, never the document text; no method of
the changed files reaches 30 lines; no `!` without a comment, no `.Result` or `.Wait()`, no `dynamic` or reflection.

**What was never run:** the single line that calls the real cloud service (`Messages.Create` of the `Anthropic` library through
`ClaudeProvider`). It was never run against the real service: the tests exercise the library's own code against canned HTTP replies, and
the cloud-on cases use a stand-in. No key exists anywhere and nothing was sent to the cloud. The first live answer from the cloud is not
verified, and the default model id (`claude-sonnet-4-20250514`) must be checked before the cloud is ever switched on.
