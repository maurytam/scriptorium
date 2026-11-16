# Data Model: Model Router

Nothing is stored. No table, column or migration changes. The types below live in memory for the length of one question. The
only stored fact used is the existing `Document.IsPrivate` (feature 001).

## Core: enums

| Type | Values | Meaning |
|---|---|---|
| `ModelPreference` | `Auto`, `Local`, `Cloud` | What the person asked for on one question. `Auto` is the default |
| `ModelBackend` | `Local`, `Cloud` | Which side answered; also what the page labels the answer with |
| `RoutingFailureKind` | `CloudNotAvailable`, `PrivateDocumentCloudRefused` | Why the router refused to choose |
| `ComplexitySignals` (`[Flags]`) | `None`, `LongQuestion`, `SeveralQuestions`, `LongReading`, `ConversationUnderWay`, `ReasoningWords` | The five signals of the approved rule, one bit each |
| `AskFailureKind` (existing) | adds `PreferenceNotAllowed` | The answer to "Cloud for a private document" (`409`). The cloud-not-available case reuses `ModelUnavailable` (`503`) |

## Core: records and options

| Type | Fields | Rules |
|---|---|---|
| `RoutingOptions` (section `Routing`) | `CloudEnabled` (bool, **false**), `ComplexityThreshold` (int, **2**) | The only switch for the cloud, and the only tunable of the complexity rule |
| `RoutingRequest` | `IsPrivate`, `Preference`, `ComplexityScore` (int) | The **four facts** of FR-012, with the cloud setting held by the router. Nothing else enters the decision |
| `ComplexityScore` | `Signals` (`ComplexitySignals`); `Value` = number of bits set | `Value` is 0 to 5. `Signals` says why |
| `RoutingResult` | success: `Backend`, `Provider`; failure: `FailureKind`, `Error` | Same pattern as `AskResult`, `AiResult`: `IsSuccess` with `MemberNotNullWhen` |

## Core: the scorer's constants

| Constant | Value | Source |
|---|---|---|
| Long question | 400 characters | approved rule, signal 1 |
| Several questions | 2 question marks | signal 2 |
| Long reading | 75 % of `Qa:MaxDocumentCharacters` (read from `QaOptions`) | signal 3 |
| Conversation under way | 3 earlier exchanges | signal 4 |
| Reasoning words | the 14 words and phrases of the fixed list (research R3) | signal 5 |

## Core: the interface

| Type | Members | Notes |
|---|---|---|
| `ICloudAIProvider : IAIProvider` | `bool IsConfigured` | `CompleteAsync` is inherited. `IsConfigured` is true only when a key is present. Written **before** `ClaudeProvider` |

## Infrastructure

| Type | Fields | Notes |
|---|---|---|
| `AnthropicOptions` (section `Anthropic`) | `ApiKey` (no default), `Model`, `TimeoutSeconds` (120), `MaxTokens` (1024) | `ApiKey` only from user-secrets or the environment, never from a committed file |
| `ClaudeProvider : ICloudAIProvider` | — | `IsConfigured` = `ApiKey` is not empty. No key → `AiResult` failure `Unavailable` before any request is built |

## Changed existing types

| Type | Change |
|---|---|
| `AskRequestDto` | new optional `Preference` (string) |
| `AskResponseDto` | new `Backend` (string: `local` or `cloud`) |
| `AskResult` | `Success(answer, truncated, backend)`; new `Backend` property |
| `DocumentQuestionService` | constructor takes `ModelRouter` and `ComplexityScorer` instead of `IAIProvider`; `AskAsync` takes `ModelPreference` |

## Decision table (what the tests walk through)

`cloud usable` = `CloudEnabled` and `IsConfigured`. "above" means the score is greater than the threshold.

| Private | Preference | Cloud usable | Score vs threshold | Outcome |
|---|---|---|---|---|
| yes | Cloud | any | any | refused (`PrivateDocumentCloudRefused`) |
| yes | Auto, Local | any | any | **Local** |
| no | Local | any | any | **Local** |
| no | Cloud | yes | any | **Cloud** |
| no | Cloud | no | any | not available (`CloudNotAvailable`) |
| no | Auto | yes | above | **Cloud** |
| no | Auto | yes | at or below | **Local** |
| no | Auto | no | any | **Local** |

## Validation

- `ComplexityThreshold` is not validated beyond being an integer (like the other options); a value of 5 or more means Auto never
  picks the cloud, a value below 0 makes every question complex.
- `preference` is validated at the endpoint (`auto`, `local`, `cloud`, any case; absent or empty = Auto); anything else is `400`.
