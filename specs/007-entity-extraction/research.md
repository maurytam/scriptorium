# Research: Entity Extraction

Decisions taken for the plan. The product decisions (five types, nothing stored, button next to Summarise, router rules, label and
notice) are the user's, in the spec's Input and Clarifications. Nothing here needs a key, a network call to the cloud or a new
package.

## R1. Share what summaries and entities have in common, instead of copying it

**Decision**: a summary and an extraction do the same first half of the work: load the ready document, refuse a text that is too
short, score it with an empty question, ask the router and, at the end, call the provider the router chose, map a provider failure
and refuse an empty reply. Only the prompt, and what is done with the reply, differ. That half moves into one small piece,
**`RoutedDocument`** (in Core):

- `RoutedDocument.PrepareAsync(repository, router, scorer, documentId, preference, tooLittleTextMessage, ct)` does the loading
  (`ReadyDocumentText`), the minimum of 50 characters, the scoring (`Score("", length, 0)`) and the routing, in that order, and
  returns itself as the result: the text, or an `AskResult` failure (`NotFound`, `NotReady`, `NotEnoughText`, `PreferenceNotAllowed`,
  `ModelUnavailable`, `Internal`).
- `routed.CompleteAsync(request, truncated, ct)` calls the chosen provider and returns an `AskResult`: the reply with the backend,
  or the failure mapped by `AskFailureMapper`; a reply that is blank is an `Internal` failure.

`DocumentSummaryService` is changed to use it (its constructor, its public method, its messages and its tests do not change) and
the new `DocumentEntityService` uses it too. The two services shrink to: prepare, build the prompt, complete (and, for entities,
read the reply).

**Rationale**: without it the new service would copy about 40 lines of the summary service (the order of checks, the scoring, the
routing call, the failure mapping, the blank check) and the two would drift apart. It is the same move feature 006 made for the
question and the summary (`ReadyDocumentText`, `AskFailureMapper`); with two identical users it now pays. The summary tests, which
use strict mocks and check the order of the checks, are the safety net for the move and are **not edited**.

**Alternatives considered**:
- *Copy the code into the new service.* Smaller diff, but two copies of the same rules (the order of checks is a privacy rule).
- *One generic "document task" service with the prompt and the reply handling passed in.* Rejected again: it hides two different
  use cases behind one parameterised class and changes constructors and tests.
- *Make the question service share it too.* Rejected: a question has a question to validate and to score and a history; it does
  not fit and is left alone.

## R2. How the model is asked to answer: one entity per line, not JSON

**Decision**: the prompt asks for **one entity per line**, as `kind: text`, with five fixed kinds (`person`, `organization`,
`date`, `amount`, `place`), no Markdown and nothing else, and the single word `NONE` when there is nothing. Example reply:

```text
organization: Alfa Srl
person: Mario Rossi
date: 12 March 2026
amount: 15,000 euro
place: Turin
```

Core reads it with `EntityReplyParser`: each line is trimmed, a leading bullet or number is dropped, the text before the first
colon is matched to one of the five kinds (any letter case, singular or plural, English), anything that does not fit is ignored.

**Rationale**: a 4 billion parameter model gets JSON wrong often (a fence of Markdown around it, a trailing comma, a missing
quote), and one broken character loses the whole answer. A line format loses only the line that is wrong, needs no JSON parsing
(no reflection, no extra code path) and survives a model that adds a friendly sentence in front. It also needs **no change to
`AiRequest` or to the providers**: a structured-output option would have to be added to `AiRequest`, to `OllamaProvider` and to
`ClaudeProvider`, whose one real call has never run.

**Alternatives considered**:
- *JSON, with the provider asked for JSON mode.* Rejected for the reasons above and because it touches both providers.
- *JSON parsed leniently.* More code to guess what the model meant.
- *A numbered list per type.* The model repeats the heading and the numbers; more noise to strip.

## R3. "Nothing is invented": check every entity against the text that was read

**Decision**: after parsing, `EntityVerifier` keeps an entity only if its text can be found in **the part of the document that
was sent to the model** (the cut at `Qa:MaxDocumentCharacters`), comparing without letter case and with runs of spaces and line
breaks counted as one space. It also removes repeats (the same text, same normalisation, in the same type) and orders each group by
**where the entity first appears in the text**. What is shown is the spelling of the first occurrence in the text (its own letter case), not the model's
spelling, with each run of spaces or line breaks inside the entity shown as a single space: a name the text breaks over two lines
is shown on one line, never with a line break in it.

**Rationale**: this is spec FR-003 and the thing that matters most with a small model. It is a pure function on two strings, easy
to test with exact cases. Showing the text's own spelling means the person sees exactly what the document says. The order by
position does not depend on how the model happened to order its lines.

**Consequences, to be said plainly**:
- A date the model rewrites ("March 12, 2026" for "12 March 2026") is **dropped**, because it is not in the text. The prompt asks
  to copy each entity exactly as written, to make this rare; the price of never showing an invented entity is that an honest but
  reformatted one is lost.
- If every entity is dropped, or the model said `NONE`, the result is "no entities found".
- An entity found in the text only as part of a longer word is still accepted (a plain search, not a word search); a name like
  "Rossi" inside "Rossini" is a theoretical false positive that is not worth the cost of word rules in several languages.

**Alternatives considered**: *trust the model* (rejected: it is the whole point of FR-003); *match on whole words only* (rejected:
punctuation and languages make it fragile, and the benefit is tiny); *fuzzy matching* (rejected: it lets an invented variant in).

## R4. Reply that cannot be read, and reply with nothing

**Decision**:
- the reply is exactly `NONE` (any letter case, with or without a final full stop) → success with **no groups** (the page says
  that nothing was found);
- the reply has at least one readable line, but after checking none of them is in the text → success with no groups (same);
- the reply has **no readable line at all** and is not `NONE` (a refusal, a paragraph of prose, a language the parser does not
  know) → failure `Internal` with `The entities could not be extracted.`, no detail of the model's text;
- a blank reply is already refused by `RoutedDocument` (`Internal`, `The answer could not be produced.`).

**Rationale**: the person must be told the truth in both cases (spec FR-005, FR-015, SC-006): "nothing found" is an answer,
"I could not read what the model said" is a failure to retry.

## R5. How Auto scores an extraction

**Decision**: as for a summary (feature 006, R2), `ComplexityScorer.Score("", textLength, 0)`. Only the long-reading signal can
fire, the score is 0 or 1, and with the default threshold of 2 Auto always answers locally. The router is not changed.

**Rationale**: the user already chose this for summaries; entities are the same kind of request (no question text). One rule,
explained in one sentence, no new setting. This lives in `RoutedDocument` now, so it is written once.

## R6. The endpoint

**Decision**: `POST /api/documents/{id}/entities` with an optional body `{ "preference": "auto|local|cloud" }`; no body is Auto.
Success is `200 { groups, truncated, backend }`. Failures use the statuses and messages of the summary, with one new text for the
`409` "too little text". The order of checks is the summary's: preference (`400`) → document found (`404`) → ready (`409`) → enough
text (`409`) → routing (`409`/`503`) → model (`503`/`504`/`500`) → readable reply (`500`).

`POST`, not `GET`, for the same reasons as the summary: it makes the model work, takes time and could cost money once the cloud is
on. `ModelPreferenceParser` and `AskFailureResponses` (feature 006) are used as they are.

## R7. The result type

**Decision**: a new `EntityResult` in Core (success: the groups, `Truncated`, `Backend`; failure: an `AskFailureKind` and a
message), with the same shape and style as `AskResult`. A failure of `RoutedDocument` (an `AskResult`) is carried into it.
`AskFailureKind` needs **no new value**: `NotEnoughText` (added by feature 006) covers the short text.

**Rationale**: the groups are not a string, so `AskResult` does not fit; a second small result type is clearer than putting a
list in a string field. No exception crosses a layer.

## R8. The page

**Decision**: an **Extract entities** button next to **Summarise** in the question panel (order: Summarise, Extract entities,
Send), with the panel's **Answer with** selector. The result is a card **Entities** above the conversation (below the summary card
if there is one): one block per non-empty type with its heading (People, Organizations, Dates, Amounts, Places) and the entities
as a wrapping list, then the backend label; the notice of FR-007 when `truncated`; the message "No entities were found in this
document." when there are no groups. While any request works, the progress indicator shows and Send, Summarise and Extract
entities are all disabled (the panel already has one `waiting` flag). The result is cleared by opening another document, closing
the panel and **New conversation**, and a late result is dropped (the existing `pending` subscription). It is not part of the
history sent with questions. The summary and the entities are independent: asking for one does not remove the other, asking again
replaces its own card.

The panel's two "ask the server, show the result" paths (summary, entities) share one private method so the component stays small
and thin; it shows and decides nothing.

**Alternatives considered**: *a separate panel* (rejected: a third place that owns the selector, the progress and the reset rules);
*a table with a column per type* (rejected: five columns of unequal length are poor on a narrow screen; a block per type wraps).

## R9. Nothing stored, no spend, no new package

No entity, no column, no migration, no cache. No package is added to any `.csproj` or `package.json`. The real cloud service is
never called and no key is needed: the cloud-on cases use a stand-in `ICloudAIProvider`, as in features 005 and 006.
`git diff main --name-only` must show no change to a project file or a lock file and no migration.

## R10. Measuring quality honestly

The quality of the entities depends on a 4B local model. The automated tests cover everything around the model (parsing,
verification, routing, failures); whether the model finds the *right* entities is judged by reading the result on fake documents
with a known content (`facts.txt` names two people, two organizations, two dates, one amount and three places). It is recorded in
`validation-results.md` as a reading, not as a measurement, and the spec's SC-002 (nothing shown that is not in the document) is
the one that the code can guarantee.
