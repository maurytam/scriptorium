# Quickstart: Validating Summaries

## Prerequisites

- .NET 10 SDK, Node with the Angular CLI, repository built: `dotnet restore && dotnet build`, and in `src/Scriptorium.Web`:
  `npm install`.
- Automated tests: `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` (they need
  no files, no Ollama and no key).
- For the manual scenarios: Ollama running with `qwen3.5:4b`, the API on a **throwaway database and its own port** (never the one
  used for daily work), and the Angular app for the page scenarios:

  ```bash
  ASPNETCORE_URLS=http://localhost:5099 \
  ConnectionStrings__Default="Data Source=/tmp/quickstart/t.db" \
  Storage__LocalPath=/tmp/quickstart/docs \
  dotnet run --no-launch-profile --project src/Scriptorium.API
  ```

  Stop it afterwards by the process id of its own port only. To look at the page, the throwaway API must listen on the port the dev
  server proxies to (5034), so the user's own API is stopped first, **by the user**.
- Fake files only, in `~/Desktop/prove-scriptorium`: `facts.txt` (short, not private), `long.txt` (longer than 8,000 characters),
  `router-private.txt` (upload it with **Private** ticked), a new `tiny.txt` of a few words (for example "Hello there.") and a fake `broken.docx` (plain text with a `.docx` name, so it fails). **No
  real document of the user is read or used.**

## Scenario 1: A normal document is summarised locally (User Story 1)

Upload `facts.txt` (not private). Ask for a summary:

```bash
curl -s -X POST http://localhost:5099/api/documents/{id}/summary
```

**Expected**: `200` with a `summary` of one short paragraph (about three to five sentences) that matches the content of the
file, `"truncated": false` and `"backend": "local"`.

## Scenario 2: A private document is summarised locally

Same with `router-private.txt` (private), once with no body and once with `-d '{"preference":"local"}'`.

**Expected**: `200`, `"backend":"local"`, a summary of that file. Nothing is sent anywhere else.

## Scenario 3: The refusals are those of the questions (User Story 2)

```bash
curl -si -X POST http://localhost:5099/api/documents/{normal-id}/summary -H 'Content-Type: application/json' -d '{"preference":"cloud"}'
curl -si -X POST http://localhost:5099/api/documents/{private-id}/summary -H 'Content-Type: application/json' -d '{"preference":"cloud"}'
curl -si -X POST http://localhost:5099/api/documents/{normal-id}/summary -H 'Content-Type: application/json' -d '{"preference":"turbo"}'
```

**Expected**: the first `503` with *The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be
answered on this computer.*; the second `409` with *This document is private, so it can only be answered on this computer. Choose
Auto or Local.*; the third `400` with *The preference must be auto, local or cloud.* No model answers any of them (the Ollama
console shows no request).

## Scenario 4: Too little text, a document not ready, an unknown id

Upload `tiny.txt` and ask for its summary; upload `broken.docx` (fake, it ends up *failed*) and ask for its summary; then ask for a random id.

**Expected**: `409` *The document has too little text to summarise.* (no request reaches Ollama); `409` *The document is not ready. Current status: failed.*; `404` *Document not found.*

## Scenario 5: A long document says what it covered (User Story 3)

Summarise `long.txt`.

**Expected**: `200` with `"truncated": true`, a summary of the **beginning** of the file; the page shows the notice that the
summary is based only on the first part. `facts.txt` shows no notice.

## Scenario 6: The page (User Stories 1, 2 and 3)

Start the Angular app, open a ready document with **Ask** and press **Summarise**.

**Expected**: a progress indicator while the model works and the buttons disabled; then a card with the paragraph and the label
*Answered on this computer*; the notice for `long.txt`; with **Cloud** chosen the page shows the "not available" message (and the
"private" one for the private file); the selector is back on **Auto** and the summary is gone after opening another document,
closing the panel or **New conversation**; pressing **Send** with a question still works as before.

## Scenario 7: Nothing is kept

Press **Summarise** twice on the same document, then look at the database and the documents folder of the throwaway API.

**Expected**: two requests reached the model (two waits); no summary appears in the database or on disk; the document list and
extracted texts are unchanged.

## Scenario 8: Cloud switched on without a key (still no cost)

Restart the throwaway API with `Routing__CloudEnabled=true` and **no key**, then repeat scenario 3 (Cloud on the normal document)
and a summary with Auto on `long.txt`.

**Expected**: Cloud still gives the same `503`; Auto answers locally (`"backend":"local"`), whatever the score, because the cloud
without a key is not usable.

## Scenario 9: The score of a summary and the stand-in cloud (automated)

`dotnet test` runs, in `DocumentSummaryServiceTests` and `SummaryEndpointTests`, the cases with a stand-in `ICloudAIProvider`: with
the default threshold Auto keeps a summary local even for a long document; with the threshold at 0 a long document goes to the
stand-in and a short one does not; a private document and the preference Local never reach it; a stand-in that fails gives `503` or
`504` and the local model is never called.

**Expected**: every case passes and no test needs a key or the network.

## Scenario 10: No regression, no secret, no spend

```bash
dotnet test
cd src/Scriptorium.Web && npm test -- --watch=false --browsers=ChromeHeadless && npm run build
git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'
git diff main --name-only | grep -E 'csproj|package(-lock)?\.json'
```

**Expected**: all tests of features 001 to 005 still pass with the same assertions, and the new ones pass, with no new build
warning; the `git grep` finds only the code that reads the key option; the last command prints nothing (no package changed).

## What this guide cannot prove

The quality of the summaries depends on the local model: it is checked by reading them, and a small model may write a sentence more
or fewer than asked. The first summary from the real cloud model is not verified: the real service is never called.

Results of the last run are recorded in `validation-results.md` once the feature is implemented.
