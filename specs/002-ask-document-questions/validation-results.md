# Validation results: Ask Questions About a Document

Results of the polish phase, run on 2026-10-23 from the `main` branch after the merge of user story 2
(pull request #13).

## 1. Quickstart scenarios (T045)

Not run yet. They need Ollama and the real model, and they cover the failures and limits of user story 3,
so they are run after that story is implemented.

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

- T045 is still to be run (see section 1).
- The 503 and 504 answers of the contract are produced only after user story 3 (T038 to T041); until then a model
  failure is answered with `500`. The README documents `500` for now and must be updated with user story 3.
