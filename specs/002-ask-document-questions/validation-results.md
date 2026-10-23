# Validation results: Ask Questions About a Document

Results of the polish phase, run on 2026-10-23: the review (section 2) from the `main` branch after the merge of
user story 2 (pull request #13), the quickstart run (section 1) on the branch of user story 3.

## 1. Quickstart scenarios (T045)

Scenarios 1 to 8 of [quickstart.md](./quickstart.md) were run on 2026-10-23 against the real API and the **real
model** (`qwen3.5:4b` in a local Ollama), after user story 3 was implemented (branch
`003-clear-limits-and-failures`). Scenario 9, the web UI, is run by hand by the user (see section 3).

**Environment**

- A throwaway database and folder in the session's scratch directory, so the user's own documents stayed untouched.
- Four API instances on other ports (5099 normal; 5098 with `Ollama__BaseUrl=http://localhost:9`; 5097 with
  `Ollama__TimeoutSeconds=1`; 5096 with `Qa__MaxDocumentCharacters=80000`, `Ollama__NumCtx=32768` and
  `Ollama__TimeoutSeconds=600`), all on the same throwaway database.
- Invented sample files only: `facts.txt` (546 characters, an agreement between two made-up companies),
  `long.txt` (63,910 characters, with a door code in the last paragraph), `private.txt` (flagged private) and
  `broken.docx` (plain text with a `.docx` name).

| # | Scenario | What was run | Observed | Result |
|---|---|---|---|---|
| 1 | Ask and answer (SC-001) | "Who signed the agreement for Alfa Srl?" about `facts.txt` | `200`, "Mario Rossi signed the agreement for Alfa Srl.", `truncated: false`, 12.4 s (first request, model loading) | Pass |
| 2 | Answer not in the document (SC-003) | 10 questions about things the file does not mention | 10 of 10 answers said the document does not contain it; 4.8 to 6.7 s each; nothing invented | Pass |
| 3 | Follow-ups (SC-005) | A conversation of 10 exchanges ("And when did he sign it?", "Where?", "What about the other party?", "On what date?", ... "And when must it be paid?") | All 10 answers correct and in context, 1.0 to 5.5 s each; "From when does that period start?" correctly gave 14 March 2026, the date of the last signature | Pass |
| 4 | Refusals | Unknown id; blank question; 2,001 characters; 2,000 characters; incomplete history entry; a failed document | `404` "Document not found."; `400` "The question is empty."; `400` "The question is too long. The limit is 2000 characters."; `200`; `400` "Every earlier exchange needs a question and an answer."; `409` "The document is not ready. Current status: failed." | Pass, see note 1 |
| 5 | Model not available | API with Ollama at an address nobody listens on | `503` with the "Check that Ollama is running and the model is installed" message; other endpoints kept answering (`200`); the same question on an instance that reaches Ollama succeeded | Pass, see note 2 |
| 6 | Timeout | `Ollama__TimeoutSeconds=1`, question about `long.txt` | `504` "The model did not answer in time. Please try again."; the API kept serving (`GET /limits` gave `200`) | Pass |
| 7 | Long document | Question about the door code in the last paragraph of `long.txt` | Default budget: `200`, `truncated: true`, "The document does not contain it." With 80,000 characters and a 32,768-token window: `200`, `truncated: false`, "ZEBRA-4471" | Pass, see note 3 |
| 8 | Nothing leaves the machine (SC-002) | `lsof` on the API process every half second while asking about the private and a non-private document | Only `127.0.0.1:11434` (Ollama) and the API's own loopback connections; no other address, for both documents | Pass |
| 9 | The web UI | Done by hand by the user | See section 3 | Pending |

The API log of the normal instance contained no `fail` or `crit` entries.

**Notes**

1. A `409` for a document that is still *processing* was not provoked: parsing a small text file ends in
   milliseconds. That case is covered by the automated tests.
2. Ollama was not stopped, because it is the user's own server. The "unavailable" case was provoked with a wrong
   address on a second API instance instead. That the same API recovers once Ollama is back follows from the
   provider keeping no connection state; it was not watched live.
3. With the raised budget the answer took about **382 seconds** on this machine, against 4 to 12 seconds with the
   default budget. Reading the whole long document works, but it is far outside the 60-second goal of SC-001,
   which is meant for the default budget. This is the reason the default stays at 8,000 characters.

## 2. Review against Constitution VI and the logging rule (T046)

**Scope**: every production class added by this feature: `AiMessage`, `AiRequest`, `ChatExchange`, `QaOptions`,
`QuestionPrompt`, `AiResult`, `AskResult`, `AskDtos`, the enums `AiRole`, `AiFailureKind` and `AskFailureKind`,
`IAIProvider`, `QuestionPromptBuilder`, `DocumentQuestionService`, `OllamaOptions`, `OllamaProvider` and
`QuestionEndpoints`.

### Methods under 30 lines

The length of every method (from its signature to its closing brace) was measured with a script over these files.
The longest ones:

| Method | Lines |
|---|---|
| `OllamaProvider.CompleteAsync` | 25 |
| `DocumentQuestionService.AskAsync` | 22 |
| `DocumentQuestionService.LoadTextAsync` | 20 |
| `QuestionPromptBuilder.RetainRecent` | 20 |
| `QuestionPromptBuilder.Build` | 17 |
| `QuestionEndpoints.AskAsync` | 15 |
| `QuestionEndpoints.ToExchanges` | 15 |
| `QuestionPromptBuilder.FitToBudget` | 15 |

All other methods are shorter than 15 lines. **Result: pass, nothing to refactor.**

### Single responsibility

| Class | Its one job |
|---|---|
| `QuestionPromptBuilder` | Turns document text, question and history into the messages sent to the model |
| `DocumentQuestionService` | The use case: validate, load the document, ask, return the outcome |
| `OllamaProvider` | The only class that talks to Ollama (Constitution VII) |
| `QuestionEndpoints` | HTTP in and out for the ask endpoint |

The private `DocumentText` class inside `DocumentQuestionService` only carries the outcome of loading a
document's text. It is not a second responsibility. **Result: pass.**

### The question and the document text are never logged

- None of the classes of the feature uses `ILogger`, `Console` or `Trace`; a search over `src` finds logging only in
  `GlobalExceptionHandler` and `DocumentProcessingQueue`, both from feature 001.
- `DocumentProcessingQueue` logs document ids and error messages of the parsing step, not texts.
- `GlobalExceptionHandler` logs the exception object, the HTTP method and the path. The ask path throws no
  exception that carries the question or the document text: `OllamaProvider` converts the exceptions of the Ollama
  client into `AiResult` failures, and a malformed JSON body produces a framework error that names the position
  of the problem, not the value.
- The answering model is reached only through `OllamaProvider`, with the `HttpClient` it creates itself.

**Result: pass.** Residual point: if a future change logs the full `HttpRequestException` text from the Ollama
client, check that the server's error body cannot contain the prompt.

### Test parity (Constitution VIII)

Every production class of the feature has its test class: `AiResultTests`, `AskResultTests`, `AskDtosTests`,
`QuestionPromptBuilderTests`, `DocumentQuestionServiceTests`, `OllamaProviderTests`, `QuestionEndpointTests`.
The logic-free records and enums are covered through the classes that use them, as stated in `tasks.md`.
**Result: pass.**

## 3. Open points

- Scenario 9 (the web UI, including the notice, the failure messages and the character counter of user story 3)
  is for the user to run by hand in the browser.
