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
