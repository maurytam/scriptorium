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
