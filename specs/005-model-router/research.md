# Research: Model Router (cloud switched off)

Decisions taken for the plan. Each one gives what was chosen, why, and what was rejected. Nothing here needs a paid account, a
key or a call to the real cloud service.

## R1. Where the routing decision lives and what it returns

**Decision**: a Core class `ModelRouter` holds the two providers (the local `IAIProvider` and the new `ICloudAIProvider`) and
the `RoutingOptions`. `Route(RoutingRequest)` returns a `RoutingResult`: on success the chosen **backend** (`Local` or `Cloud`)
together with its **provider**; on failure a kind (`CloudNotAvailable`, `PrivateDocumentCloudRefused`) and a message.
`DocumentQuestionService` asks the router, then calls the provider it was given.

**Rules, in this order** (the first that applies wins):

1. Preference **Cloud** and the document is private → failure `PrivateDocumentCloudRefused` (FR-009), whatever the cloud setting.
2. The document is private → **Local** (FR-002).
3. Preference **Local** → **Local** (FR-004).
4. Preference **Cloud** → **Cloud** if the cloud is usable, otherwise failure `CloudNotAvailable` (FR-006, FR-008).
5. Preference **Auto** → **Cloud** if the cloud is usable **and** the complexity score is **above** the threshold; otherwise
   **Local** (FR-005).

The cloud is **usable** when `Routing:CloudEnabled` is true **and** the cloud provider says it is configured (it has a key,
FR-007). So "on without a key" counts as unavailable (spec, edge cases): an explicit Cloud choice is answered with the error, and
Auto never picks it.

**Rationale**: it is the single place that decides (constitution VII) and it depends only on four facts (FR-012), so a table of
cases tests it completely. Returning the provider with the decision keeps `DocumentQuestionService` free of any `if` on the
backend, and the same value carries the backend name for the label (R7).

**Alternatives rejected**:

- *A `RoutingAIProvider` registered as `IAIProvider` that hides the decision inside the provider:* the decision needs the
  document's privacy and the preference, which are not part of `AiRequest`; it would need a side channel, and
  `AiConfigurationTests` (which expects `IAIProvider` to be the Ollama provider) would break.
- *The service picking the provider with its own `if`:* spreads the privacy rule into a second place.
- *Falling back to Ollama when the cloud fails:* rejected by the user (spec Q1); an explicit error is shown instead.

## R2. The cloud provider and its seam

**Decision**: `ICloudAIProvider : IAIProvider` adds one member, `bool IsConfigured`. `ClaudeProvider` (Infrastructure) is its
only implementation and the only class that knows the cloud library, exactly as `OllamaProvider` is for Ollama. It is registered
as `ICloudAIProvider`; `IAIProvider` stays the Ollama provider, so every existing test that replaces or resolves `IAIProvider`
keeps working.

`ClaudeProvider` follows the shape of `OllamaProvider`: it turns an `AiRequest` into the library's request, enforces its own
timeout, and turns every expected problem into an `AiResult` failure (`Unavailable` when there is no key or the service cannot be
reached, `Timeout`, `Failed`) with its own wording ("cloud model", not "Ollama"). With **no key** it returns `Unavailable`
**before building any request**, so nothing is ever sent.

**How it is tested without the real service**: the library is reached through one small internal seam (the way
`OllamaProvider` uses `IOllamaApiClient`), so the tests use a Moq stand-in or, if the library accepts a custom `HttpClient`, a
fake `HttpMessageHandler` that returns a canned reply and fails the test if it is ever asked for anything while the cloud is
off. Which of the two is possible depends on the library's public surface, which is **checked when the package is added**
(task), not guessed now. In both cases no test reaches the network.

**Outcome of the check (T014, 2026-11-16, read from the library's XML documentation and the names in its binary; nothing was
run)**: the library accepts a custom `HttpClient` (`ClientOptions.HttpClient`), so the **fake `HttpMessageHandler`** way is
used: the real library code runs against canned replies and no test reaches the network. A small extra interface is not
needed. Facts that shape `ClaudeProvider`:

- **The key defaults to the `ANTHROPIC_API_KEY` environment variable** when none is given. The provider therefore creates the
  client **lazily and only when `Anthropic:ApiKey` has a value**, and passes that value explicitly, so a key that happens to
  exist in the owner's shell is never used (this is the point U2 of the analysis).
- The library's own timeout defaults to 10 minutes and it **retries** connection errors, timeouts, `408`, `409`, `429` and
  `5xx`. The provider sets the timeout to `Anthropic:TimeoutSeconds` and `MaxRetries` to 0, so a question is one request.
- A custom `HttpClient` has its own 100-second timeout that can cut a request short: the provider gives it an infinite one, as
  `OllamaProvider` does, and enforces the configured timeout itself.
- Failures: `AnthropicException` is the base type, with `AnthropicIOException` (connection) and `AnthropicApiException` (an HTTP
  error status); a timeout is a `TaskCanceledException`. The mapping is: connection problems and `401`/`403` →
  `Unavailable`; a timeout while the caller did not cancel → `Timeout`; any other library failure or an empty reply →
  `Failed`.
- The exact members used to build the request (`Messages.Create`, `MessageCreateParams`) are confirmed when `ClaudeProvider` is
  compiled (T017).

The claim "the single line that calls the real service was never run" stays true but is narrower than first thought: the
library's request, parsing and error handling **are** exercised against canned replies; only the real network exchange and the
real credentials are not.

**What this cannot prove** (said openly): the line that calls the real service is compiled and unit-tested up to the seam, but
it runs for real only the day the owner switches the cloud on with a key. That is the price of spending nothing, and the
quickstart says so.

**Alternatives rejected**: calling the service with `HttpClient` directly (forbidden by the constitution); a second class that
wraps the provider only for tests (no gain over the seam).

## R3. The complexity rule (approved by the user, 2026-11-16)

**Decision**: a small Core class `ComplexityScorer` computes a score from **0 to 5**: one point for each of five signals that is
present. The result keeps *which* signals fired (a `[Flags]` enum, `ComplexitySignals`) and the score is their count, so a test
or a log line can say why.

| # | Signal | Present when | Input it uses |
|---|---|---|---|
| 1 | Long question | the trimmed question has **at least 400** characters | question text |
| 2 | Several questions | the question has **at least 2** `?` | question text |
| 3 | Long reading | the part of the document the model reads reaches **75 % of `Qa:MaxDocumentCharacters`** (6,000 of 8,000 by default) | **length** of the document text only |
| 4 | Conversation under way | there are **at least 3** earlier exchanges | number of earlier exchanges |
| 5 | Reasoning words | the question contains one of the fixed words or phrases below | question text |

**Fixed list (English and Italian; *perchè* with the grave accent added at the user's request on 2026-11-16), in code, not in configuration, no stemming**: `compare`, `explain why`, `analyze`,
`differences`, `implications`, `pros and cons`, `confronta`, `perche`, `perché`, `perchè`, `analizza`, `differenze`, `implicazioni`,
`pro e contro`. Matching is case-insensitive and on **whole words or phrases** (so `compare` does not fire inside another word),
using culture-invariant comparison.

**A question is complex when the score is *above* the threshold.** `Routing:ComplexityThreshold` is configurable, default **2**
(so at least three signals); a score **equal** to the threshold stays local (spec, edge cases). Only the threshold is
configurable; the five signal limits and the word list are named constants with tests.

**Privacy**: the scorer receives the question and *numbers* (the document's length, the count of earlier exchanges). It never
receives, and so never looks at, the document text (spec edge case).

**Document length used for signal 3**: the length the model reads is `min(text length, Qa:MaxDocumentCharacters)`; the scorer
clamps the length it is given, so it needs no second cut and does not depend on `TextExcerpt` (a one-character difference when
the cut avoids splitting a character cannot change the 75 % test in any realistic setting).

**Rationale**: deterministic, explainable in a sentence (FR-012), cheap (the question is at most 2,000 characters), no extra
model call.

**Alternatives rejected** (by the user and in the analysis): asking the local model to classify the question (extra call, not
deterministic); using only the length (too coarse); stemming or a configurable word list (not wanted for now).

**Known limits**: the list covers English and Italian only and is extended by hand; a rephrased question can miss it. With the
cloud off the score changes nothing, so a miss has no effect until the cloud is switched on, and the threshold is the first
thing to tune then.

## R4. The package that makes the cloud provider possible

**Decision**: add one NuGet package to `Scriptorium.Infrastructure`: **`Anthropic`**, the official .NET library published by
Anthropic. Approved by the user on 2026-11-16 on the condition that it is free.

**Checked on 2026-11-16 on the public NuGet index (nothing downloaded or installed)**:

| | |
|---|---|
| Package | `Anthropic` (the independent `Anthropic.SDK` is **not** the one chosen) |
| Publisher | Anthropic |
| Version to add | **13.8.0** (latest at the time; pinned in the project file) |
| Licence | **Apache-2.0** for 13.8.0 (an older version, 12.37.0, was MIT). Open source, no fee |
| Cost | The library is free. Only *calls to the service* are paid, and none is ever made while the cloud is off |
| Targets | `net8.0`, `net9.0`, `netstandard2.0` (a `net10.0` project uses the `net9.0` set) |

**What it brings in (checked on 2026-11-16 after adding the package to the project)**: **nothing that was not already there.**
The package manifest lists `Microsoft.Extensions.AI.Abstractions` 10.5.1, `System.Net.ServerSentEvents` 10.0.1 and
`System.Text.Json` 10.0.6, but on this project:

| Dependency | What really happens |
|---|---|
| `Microsoft.Extensions.AI.Abstractions` | Resolved to **10.8.0**, which was **already** in the solution through `OllamaSharp` 5.5.0; `Anthropic` reuses it |
| `Microsoft.Extensions.AI` | 10.8.0, already there through `OllamaSharp`; not brought by `Anthropic` |
| `System.Net.ServerSentEvents`, `System.Text.Json` | Not added as packages: .NET 10 provides them in the framework |

The list of transitive packages is the one of before plus `Anthropic` itself. `dotnet list ... --include-transitive` and
`dotnet nuget why` were used for the check; `git diff` shows one changed line, in `Scriptorium.Infrastructure.csproj`. **Nothing
else is added without asking** (constitution, non-negotiable constraints). No npm package.

**Alternatives rejected**: `Anthropic.SDK` (unofficial); writing the HTTP calls by hand (forbidden outside a provider and more
code to keep).

## R5. Configuration and the key

**Decision**: two sections.

- `Routing` (Core, `RoutingOptions`): `CloudEnabled` (default **false**), `ComplexityThreshold` (default **2**).
- `Anthropic` (Infrastructure, `AnthropicOptions`): `ApiKey` (no default, **never in a committed file**), `Model` (default
  `claude-sonnet-4-20250514`, the value in `CLAUDE.md`), `TimeoutSeconds` (120, like Ollama), `MaxTokens` (1024).

The key is set with `dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project src/Scriptorium.API` (already the project's
rule) or an environment variable. `appsettings.json` gets the `Routing` section and the `Anthropic` section **without** a key.
The delivered file has `CloudEnabled: false`, so the application needs no key and no network to be built, tested and used
(FR-007, SC-006).

**Points to confirm before the cloud is ever switched on** (not now): that the default model id is still the one to use; the
value above is the one written in `CLAUDE.md`, and it is never sent while the cloud is off.

## R6. How the preference and the failures travel over HTTP

**Decision**: `POST /api/documents/{id}/ask` gets an optional `preference` field (`"auto"`, `"local"`, `"cloud"`, any case).
Absent or empty means Auto. Any other value is **`400`** with `The preference must be auto, local or cloud.`; no model is
called. The endpoint parses it (it already maps the DTO's history), so the Core receives a `ModelPreference` enum.

| Situation | Status | Message (shown by the page as it is) |
|---|---|---|
| Cloud chosen, cloud off or without a key | `503` | The cloud model is not available. It is switched off or not set up. Choose Auto or Local to be answered on this computer. |
| Cloud chosen for a private document | `409` | This document is private, so it can only be answered on this computer. Choose Auto or Local. |
| Cloud on, no answer in time | `504` | The cloud model did not answer in time. Please try again. |
| Cloud on, service cannot be reached | `503` | The cloud model is not available. Check the cloud settings and your connection. |

The statuses are already in the list whose server text the page shows (`400, 409, 503, 504`), so the existing
`describeAskFailure` needs **no change**. The success response gets a new field **`backend`** (`"local"` or `"cloud"`); the
existing fields are untouched, so an old client keeps working.

**Rationale**: reuses existing statuses and the existing page logic; `409` fits "the request conflicts with this document's
state", `503` is what the model-unavailable case already uses.

**Alternatives rejected**: binding the preference as an enum in the request body (an unknown value would give the framework's
generic `400` without a useful message); a new status code.

## R7. What the page shows

**Decision**: in the question panel, a small **Answer with** selector (Auto, Local, Cloud) next to the Send button, Auto by
default and **reset to Auto** whenever the conversation is reset (another document, close, new conversation), so a stale Cloud
choice never carries over. The Cloud choice stays selectable even when the cloud is off (spec Q1, option A: the explicit
error, not a disabled option). Each answer carries a small label: **"Answered on this computer"** or **"Answered by the cloud
model"**, taken from `backend`. The introductory hint ("…produced on your computer") is reworded so that it stays true when the
cloud is on: it says that answers are produced on this computer unless the cloud is chosen or picked by Auto.

The page decides nothing about routing (FR-016): it sends the preference and shows what the server says.

## R8. Keeping the question feature (002) unchanged for the user

**Decision**: behaviour for a person with the delivered settings is identical: every question goes to Ollama with the same
prompt, limits, notices and messages. What changes in code is limited to the places the router needs:

- `DocumentQuestionService`: its constructor receives the router and the scorer instead of the provider; `AskAsync` receives the
  `ModelPreference` (before the cancellation token); it reads the document's privacy and calls the provider the router returns.
- `AskResult.Success` carries the backend; `AskResponseDto` gets `backend`; `AskRequestDto` gets `preference`.
- `Program.cs` registers the new pieces; `IAIProvider` is still the Ollama provider.

**Consequence for SC-003 (accepted by the user on 2026-11-16, and the spec's wording was adjusted)**: every *existing test case keeps its assertions* and must still pass, but the
object under test is built differently, so the **set-up line** of `DocumentQuestionServiceTests` (and the calls of `AskAsync`,
which gain the preference argument) change mechanically. The wording of SC-003 ("without being changed") is stricter than what
a constructor change allows; see the plan, "Points for the user".

**Alternatives rejected**: keeping the old constructor next to a new one only for the tests; an optional constructor parameter
defaulting to "no routing" (a dependency that can be silently missing).

## R9. Documentation to update

`README.md` (the roadmap row, the configuration table, the ask endpoint line), `docs/architecture.md` (section 7 "Planned: the
model router" becomes a description of what exists, with the cloud off), and the feature 002 contracts are left as they were:
the new fields are described in this feature's contracts.
