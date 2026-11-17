# Quickstart: Validating Entity Extraction

## Prerequisites

- .NET 10 SDK, Node with the Angular CLI, repository built: `dotnet restore && dotnet build`, and in `src/Scriptorium.Web`:
  `npm install`.
- Automated tests: `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` (they need
  no files, no Ollama and no key).
- For the manual scenarios: Ollama running with `qwen3.5:4b`, the API on a **throwaway database and its own port** (never the one
  used for daily work), and, for the page scenarios, a separate Angular dev server on its own port whose proxy points to the
  throwaway API (a small proxy file kept outside the repository), so the user's own API (5034) and page (4200) are not touched:

  ```bash
  ASPNETCORE_URLS=http://localhost:5099 \
  ConnectionStrings__Default="Data Source=/tmp/quickstart/t.db" \
  Storage__LocalPath=/tmp/quickstart/docs \
  Anthropic__ApiKey="" \
  dotnet run --no-launch-profile --project src/Scriptorium.API
  ```

  Stop it afterwards by the process id of its own port only.
- Fake files only, in `~/Desktop/prove-scriptorium`: `facts.txt` (short, not private, names two people, two organizations, two
  dates, one amount and three places), `router-private.txt` (upload it as **private**), `long.txt` (longer than 8,000 characters),
  `tiny.txt` ("Hello there.") and the fake `broken.docx`. **No real document of the user is read or used.**
- **After uploading, list the documents and check `isPrivate`** of `router-private.txt` (once an upload silently lost the flag and
  the first check proved nothing):

  ```bash
  curl -s -F "file=@$HOME/Desktop/prove-scriptorium/router-private.txt" -F "isPrivate=true" http://localhost:5099/api/documents
  curl -s http://localhost:5099/api/documents      # router-private.txt must say "isPrivate":true
  ```

## Scenario 1: A normal document gives its entities, grouped (User Story 1)

Upload `facts.txt` (not private). Ask for its entities:

```bash
curl -s -X POST http://localhost:5099/api/documents/{id}/entities
```

**Expected**: `200` with `groups` in the order people, organizations, dates, amounts, places, each type present with the right
names (Mario Rossi and Anna Bianchi; Alfa Srl and Beta Spa; 12 March 2026 and 14 March 2026; 15,000 euro; Turin, Milan and
Bologna), `"truncated": false`, `"backend": "local"`. The quality is **read**, not measured: a small model may miss one or add a
wrong-looking one; every entity shown must be in the file (SC-002).

## Scenario 2: A private document is processed locally

Same with `router-private.txt` (private), once with no body and once with `-d '{"preference":"local"}'`.

**Expected**: `200`, `"backend":"local"`, the entities of that file. Nothing is sent anywhere else.

## Scenario 3: The refusals are those of the questions (User Story 2)

```bash
curl -si -X POST http://localhost:5099/api/documents/{normal-id}/entities -H 'Content-Type: application/json' -d '{"preference":"cloud"}'
curl -si -X POST http://localhost:5099/api/documents/{private-id}/entities -H 'Content-Type: application/json' -d '{"preference":"cloud"}'
curl -si -X POST http://localhost:5099/api/documents/{normal-id}/entities -H 'Content-Type: application/json' -d '{"preference":"turbo"}'
```

**Expected**: the first `503` with *The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be
answered on this computer.*; the second `409` with *This document is private, so it can only be answered on this computer. Choose
Auto or Local.*; the third `400` with *The preference must be auto, local or cloud.* No model answers any of them.

## Scenario 4: Too little text, a document not ready, an unknown id

Upload `tiny.txt` and ask for its entities; upload `broken.docx` (it ends up *failed*) and ask; then ask for a random id.

**Expected**: `409` *The document has too little text to extract entities from.* (no request reaches Ollama); `409` *The document
is not ready. Current status: failed.*; `404` *Document not found.*

## Scenario 5: A long document says what it covers (User Story 3)

```bash
curl -s -X POST http://localhost:5099/api/documents/{long-id}/entities
curl -s -X POST http://localhost:5099/api/documents/{facts-id}/entities
```

**Expected**: `"truncated": true` for `long.txt` (63,910 characters, 8,000 read) and `false` for `facts.txt`. `long.txt` mentions no
entity (it says so in every section), so the first call may also return `"groups": []`; both are correct.

## Scenario 6: The page (User Stories 1, 2 and 3)

Start the throwaway API and the separate Angular server, open a ready document with **Ask** and press **Extract entities**.

**Expected**: a progress indicator while the model works and **Summarise**, **Extract entities** and **Send** disabled; then a
card **Entities** with a heading and a list per type found (no empty heading), and the label *Answered on this computer*; for
`long.txt` the notice *These entities are based only on the first part of the document, which is too long to be read in full.*,
and the message *No entities were found in this document.* if the list is empty; with **Cloud** chosen the "not available"
message (and the "private" one for the private file) with no card; the selector is back on **Auto** and the card is gone after
opening another document, closing the panel or **New conversation**; **Summarise** and **Send** still work, and a summary and
the entities can be on the screen together.

## Scenario 7: Nothing is kept, nothing is invented

Press **Extract entities** twice on the same document, then look at the database and the documents folder of the throwaway API.
Then read the entities shown for `facts.txt` and `router-private.txt` against the files.

**Expected**: two requests reached the model (two waits); nothing about entities in the database or on disk; the document list and
extracted texts unchanged; every entity shown is written in the file.

## Scenario 8: Cloud switched on without a key (still no cost)

Restart the throwaway API with `Routing__CloudEnabled=true` and an **empty key**, then repeat scenario 3 (Cloud on the normal
document) and ask for the entities of `long.txt` with Auto.

**Expected**: Cloud still gives the same `503`; Auto answers locally (`"backend":"local"`), whatever the score, because the cloud
without a key is not usable.

## Scenario 9: The reply of the model and the stand-in cloud (automated)

`dotnet test` runs, in `EntityReplyParserTests`, `EntityVerifierTests`, `DocumentEntityServiceTests` and `EntityEndpointTests`,
the cases that no manual run can force: a reply of only `NONE`; a reply with a friendly sentence around the lines; kinds in any
letter case and in the plural; a reply with no readable line (a `500` with *The entities could not be extracted.* and no detail);
an entity the model invented (dropped); an entity that appears twice or in another letter case (shown once, as the text writes it);
the order of appearance; and, with a stand-in `ICloudAIProvider`, the same routing table as for summaries.

**Expected**: every case passes and no test needs a key or the network.

## Scenario 10: No regression, no secret, no spend

```bash
dotnet test
cd src/Scriptorium.Web && npm test -- --watch=false --browsers=ChromeHeadless && npm run build
git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'
git diff main --name-only | grep -E 'csproj|package(-lock)?\.json|Migrations'
```

**Expected**: all tests of features 001 to 006 still pass with the same assertions (`DocumentSummaryServiceTests` and
`SummaryEndpointTests` are **not edited**), and the new ones pass, with no new build warning; the `git grep` finds only the code
that reads the key option; the last command prints nothing (no package or migration changed).

## What this guide cannot prove

The quality of the entities depends on the local model: it is checked by reading them, and a small model may miss an entity or
copy one badly (and a badly copied one is dropped, not shown). The first extraction from the real cloud model is not verified: the
real service is never called.

Results of the last run are recorded in `validation-results.md` once the feature is implemented.
