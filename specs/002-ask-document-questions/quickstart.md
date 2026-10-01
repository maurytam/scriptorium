# Quickstart: Validating "Ask Questions About a Document"

Runnable scenarios that prove the feature end to end. Endpoint shapes are in
[contracts/ask-api.md](./contracts/ask-api.md); the types involved in [data-model.md](./data-model.md).
Automated tests cover the logic with a fake model; the scenarios below need the **real** local model, because
speed and answer quality cannot be tested without it.

## Prerequisites

- Everything from the [feature 001 quickstart](../001-document-upload-parsing/quickstart.md).
- Ollama running on the same machine (`ollama serve`, or the desktop app) and the model installed:
  `ollama pull qwen3.5:9b`. Check with `curl http://localhost:11434/api/tags`.
- API running against a throwaway database and folder (so your own documents stay untouched):

```bash
ConnectionStrings__Default="Data Source=/tmp/qa/t.db" Storage__LocalPath=/tmp/qa/docs \
dotnet run --project src/Scriptorium.API
```

Sample files: a short `.txt` of **under 4,000 characters** with a few clear facts (names, dates, amounts), so it is not truncated; a long `.txt` of at least
60,000 characters whose last paragraph contains a distinctive fact; a `.txt` marked private.

## Scenario 1 — Ask and answer (User Story 1)

```bash
curl -s -F "file=@facts.txt" http://localhost:5034/api/documents        # note the id, wait until "ready"
curl -s -X POST http://localhost:5034/api/documents/{id}/ask \
  -H 'Content-Type: application/json' -d '{"question":"Who signed the agreement?"}'
```

**Expected**: `200` with an `answer` stating the fact from the file and `truncated: false`; the answer
arrives within 60 seconds (SC-001).

## Scenario 2 — The answer is not in the document (FR-003, SC-003)

Ask about something the file does not mention, for example a fact about a different subject.

**Expected**: `200` with an `answer` that says the document does not contain it, not an invented fact.
Repeat with 10 such questions: at least 9 must say so (SC-003).

## Scenario 3 — Follow-up questions (User Story 2, SC-005)

```bash
curl -s -X POST http://localhost:5034/api/documents/{id}/ask -H 'Content-Type: application/json' \
  -d '{"question":"And when was it signed?","history":[{"question":"Who signed the agreement?","answer":"..."}]}'
```

**Expected**: the follow-up is answered in the context of the first exchange. Build a conversation of 10
exchanges and check that the last answers still make sense without restating the context.

## Scenario 4 — Refusals (FR-002, FR-010)

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST .../api/documents/{processing-or-failed-id}/ask -H 'Content-Type: application/json' -d '{"question":"hi"}'   # 409
curl -s -o /dev/null -w "%{http_code}\n" -X POST .../api/documents/00000000-0000-0000-0000-000000000000/ask -H 'Content-Type: application/json' -d '{"question":"hi"}'   # 404
curl -s -o /dev/null -w "%{http_code}\n" -X POST .../api/documents/{id}/ask -H 'Content-Type: application/json' -d '{"question":"   "}'   # 400
```

**Expected**: `409`, `404` and `400`, each with a clear message. A question of 2,001 characters also gets `400`.

## Scenario 5 — Model not available (FR-009, SC-004)

Stop Ollama (or start the API with `Ollama__BaseUrl=http://localhost:9`), then ask a question.

**Expected**: `503` with the message about starting Ollama and installing the model. Start Ollama again:
the same question now succeeds without restarting the API.

## Scenario 6 — Timeout (FR-009)

Start the API with `Ollama__TimeoutSeconds=1` and ask a question about the long document.

**Expected**: `504` with the "try again" message; the API keeps serving other requests.

## Scenario 7 — Long document (FR-008)

Ask about the distinctive fact in the **last** paragraph of the long file.

**Expected**: `200` with `truncated: true`; the answer says the document does not cover it, because only the
beginning was read. Raise `Qa__MaxDocumentCharacters` and `Ollama__NumCtx` and repeat: the fact is found. This
scenario also confirms how Ollama behaves with a prompt that exceeds its window (research R5).

## Scenario 8 — Nothing leaves the machine (FR-004, SC-002)

While asking questions about the private file **and** about a non-private one, list the connections of the API
and Ollama processes:

```bash
lsof -nP -i -a -p "$(pgrep -f Scriptorium.API | head -1)"        # only 127.0.0.1 / ::1 expected
```

**Expected**: no connection to a non-local address, for both documents.

## Scenario 9 — The web UI (all user stories)

Open the app, click **Ask** on a ready document and:

1. Ask a question: a progress indicator appears at once, then the answer.
2. Ask a follow-up; ask 10 in a row.
3. Ask about a long document: the "based only on the first part" notice appears with the answer.
4. Stop Ollama and ask: a specific message appears, the question is still in the input, and **Send** works again
   once Ollama is back.
5. Upload and delete other documents while an answer is being prepared: they work normally (SC-006).
6. Delete the open document, choose another one, or reload: the conversation disappears.

## Notes

- Results of the run are recorded in `validation-results.md`, as for feature 001.
- Answer wording varies from run to run; judge the content, not the exact text.
