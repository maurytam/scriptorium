# Quickstart: Validating the Model Router

Nothing here needs a key, an account or a paid service. The cloud stays **off** in every manual scenario; the cloud-on
behaviour is proved by the automated tests with a stand-in, never against the real service.

## Prerequisites

- .NET 10 SDK, Node with the Angular CLI, repository built: `dotnet restore && dotnet build`, and in `src/Scriptorium.Web`:
  `npm install`.
- Automated tests: `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless` (they need
  no files, no Ollama and no key).
- For the manual scenarios: Ollama running with `qwen3.5:4b` (scenarios 1 and 2), the API on a throwaway database and its own
  port (not the one used for daily work), and the Angular app in a browser:

  ```bash
  ASPNETCORE_URLS=http://localhost:5099 \
  ConnectionStrings__Default="Data Source=/tmp/quickstart/t.db" \
  Storage__LocalPath=/tmp/quickstart/docs \
  dotnet run --no-launch-profile --project src/Scriptorium.API
  ```

- Fake files, **different content each**: a short `.txt` (not private) and a second short `.txt` uploaded with the **Private** box
  ticked. **No real user document is used.**

## Scenario 1: Everything goes local by default (User Story 1)

Ask the non-private document a question with the selector on **Auto** (and without touching it).

**Expected**: the answer comes from Ollama as before, with the label **Answered on this computer**; limits, notices and error
messages are those of feature 002. Asking the same with the selector on **Local** gives the same.

## Scenario 2: A private document stays local (User Story 1)

Ask the private document, with Auto, then with Local.

**Expected**: answered on this computer, label **Answered on this computer**.

## Scenario 3: Cloud chosen while it is off (User Story 2)

Pick **Cloud** on the non-private document and send a question.

**Expected**: no answer; the panel shows *The cloud model is not available. It is switched off or not set up. Choose Auto or
Local to be answered on this computer.* The Cloud choice is selectable (it is not greyed out). Picking Auto afterwards and asking
works. Check on the API console that nothing was sent anywhere.

## Scenario 4: Cloud chosen for a private document (User Story 2)

Pick **Cloud** on the private document and send a question.

**Expected**: no answer; the panel shows *This document is private, so it can only be answered on this computer. Choose Auto or
Local.* Ollama is not asked (its console shows no request).

## Scenario 5: The selector starts from Auto

Choose Cloud, close the panel, open a document again.

**Expected**: the selector is back on **Auto**.

## Scenario 6: The endpoint

```bash
curl -s -X POST http://localhost:5099/api/documents/{id}/ask -H 'Content-Type: application/json' \
  -d '{"question":"What is this about?","preference":"local"}'
curl -si -X POST http://localhost:5099/api/documents/{id}/ask -H 'Content-Type: application/json' \
  -d '{"question":"hi","preference":"turbo"}'
curl -si -X POST http://localhost:5099/api/documents/{id}/ask -H 'Content-Type: application/json' \
  -d '{"question":"hi","preference":"CLOUD"}'
```

**Expected**: the first gives `200` with `"backend":"local"`; the second `400` `The preference must be auto, local or cloud.`;
the third `503` with the cloud-not-available message (for a private id: `409`). See
[contracts/ask-routing-api.md](./contracts/ask-routing-api.md).

## Scenario 7: Cloud switched on without a key (still no cost)

Restart the throwaway API with `Routing__CloudEnabled=true` and **no key**.

**Expected**: with **Cloud** chosen on a non-private document: `503` *The cloud model is not available…* and nothing is sent.
With **Auto** and a complex question (for example a question with several `?`, a word such as "compare" and an earlier
conversation of three exchanges on a long document): answered locally, because a cloud without a key is unusable.

## Scenario 8: The decision table and the stand-in cloud (automated)

`dotnet test` runs the whole table of [data-model.md](./data-model.md) (private × preference × cloud off, on, on without a key ×
score below, at and above the threshold) and the cloud-on cases with a stand-in provider that never reaches the network.

**Expected**: every case gives the outcome of the table; the score of the examples of the spec is 1, 2 and 4; a score equal to the
threshold stays local; a test fails if any code path reaches the real service.

## Scenario 9: No regression

```bash
dotnet test
cd src/Scriptorium.Web && npm test -- --watch=false --browsers=ChromeHeadless && npm run build
```

**Expected**: all tests of features 001 to 004 still pass with the same assertions, and the new ones pass, with no new build
warning. `dotnet list package --include-transitive` shows `Anthropic` 13.8.0 and only the dependencies of
[research.md](./research.md) R4.

## Scenario 10: No secret, no spend

```bash
git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'
```

**Expected**: only code that *reads* the key option; no key value anywhere in the repository; `appsettings.json` has
`"CloudEnabled": false` and no `ApiKey`.

## What this guide cannot prove

The single line that calls the real cloud service is compiled and tested up to its seam, but it runs for real only when the owner
switches the cloud on with a key. Until then the router, the preference, the messages and every refusal are verified; the first
live answer from the cloud is not.

Results of the last run are recorded in `validation-results.md` once the feature is implemented.
