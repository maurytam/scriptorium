# Data Model: Entity Extraction

**No stored data and no migration.** The entities are produced from existing data and sent back; they are not kept anywhere.

## The entities (transient)

What one request produces. It exists only in the response and on the page.

| Field | Meaning |
|---|---|
| `Groups` | The non-empty groups, in the fixed order People, Organizations, Dates, Amounts, Places. Empty when nothing was found |
| `Group.Type` | One of five fixed types |
| `Group.Entities` | The entities of that type: no repeats, each written as in the document (its own letter case, with any run of spaces or line breaks inside it shown as one space), in the order of their first appearance in the text read |
| `Truncated` | True only when the document text is longer than `Qa:MaxDocumentCharacters`, so the entities come from the first part |
| `Backend` | `local` (Ollama, on this computer) or `cloud`; always `local` with the delivered settings |

## Types in Core

```text
EntityType            enum: Person, Organization, Date, Amount, Place          // the order is the order of display
EntityCandidate       ( EntityType Type, string Text )                          // one line of the model's reply, not yet checked
EntityGroup           ( EntityType Type, IReadOnlyList<string> Entities )       // checked, without repeats, in order
EntityPrompt          ( AiRequest Request, bool Truncated, string TextRead )    // the request, and the exact text the model was given
EntityReply           ( EntityReplyKind Kind, IReadOnlyList<EntityCandidate> Candidates )   // what the parser returns; Kind: Entities | None | Unreadable
EntityResult          success: ( Groups, Truncated, Backend )  |  failure: ( AskFailureKind, Error )
```

`EntityPrompt.TextRead` is the cut of the document that went into the request. The verifier checks every entity against **that**
text, so an entity from the part the model did not read can never be shown.

## Failure kinds

`AskFailureKind` is **unchanged** (`NotEnoughText` already exists since feature 006). An extraction can fail with:

| Kind | When | HTTP |
|---|---|---|
| `NotFound` | no such document | 404 |
| `NotReady` | document processing or failed | 409 |
| `NotEnoughText` | trimmed text under 50 characters | 409 |
| `PreferenceNotAllowed` | Cloud chosen for a private document | 409 |
| `ModelUnavailable` | Cloud chosen while off or without a key, or the model not reachable | 503 |
| `Timeout` | no answer in time | 504 |
| `Internal` | any other model failure, a blank reply, or a reply with no readable line | 500 |

"Nothing found" is **not** a failure: it is a success with no groups.

## Request and response shapes

```text
EntityRequestDto    ( string? Preference = null )                                   // optional body; null or empty = Auto
EntityGroupDto      ( string Type, IReadOnlyList<string> Entities )                 // Type: people | organizations | dates | amounts | places
EntityResponseDto   ( IReadOnlyList<EntityGroupDto> Groups, bool Truncated, string Backend = "local" )
```

## The model's reply (text), and how it is read

```text
person: Mario Rossi          kinds, any case, singular or plural: person(s)/people, organization(s), date(s), amount(s), place(s)
organization: Alfa Srl       one entity per line; a bullet or a number in front of a line is ignored
NONE                         the whole reply, when there is nothing
```

Lines that are not `kind: text` with a known kind are ignored. See research R2 to R4 for what happens with an empty, unreadable or
unverifiable reply.

## The router and the scorer

Unchanged. `RoutedDocument` asks `ComplexityScorer.Score("", text.Length, 0)` and `ModelRouter.Route(new RoutingRequest(
isPrivate, preference, score))`, for entities exactly as for summaries. The decision table of feature 005 applies as it is; the
score is only ever 0 or 1 (feature 006, R2).

## Constants

| Constant | Value | Where | Why |
|---|---|---|---|
| Minimum text | 50 characters (trimmed) | `RoutedDocument` (moved from `DocumentSummaryService`) | below this there is nothing to extract; shared with summaries |
| The five types and their words | person, organization, date, amount, place | `EntityType` and `EntityReplyParser` | the user's choice (spec FR-002) |
| Characters read | `Qa:MaxDocumentCharacters` (8000) | existing setting | the same part the questions and summaries read |

No new setting.
