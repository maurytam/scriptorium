# Research: Extracted-Text Popup

All questions below are closed; none is left as NEEDS CLARIFICATION. They were answered by reading the current code
(`DocumentEndpoints`, `QuestionPromptBuilder`, `QaOptions`, the Angular list, question panel and API service), not by running anything.

## R1. Where is the text cut: in the server or in the browser?

**Decision**: In the server. A new use case reads the stored text, cuts it at `Qa:MaxDocumentCharacters` and returns the cut
text together with `truncated` and `totalCharacters`.

**Rationale**:
- The limit is a server setting (`Qa:MaxDocumentCharacters`). The spec (FR-008) wants the popup to follow it, so the server is
  the one place that knows it.
- A document can be up to 50 MB (the upload limit); its text can be very large. Cutting in the browser would send all of it to
  show 8,000 characters.
- The cut that the model reads is already made in the server (`QuestionPromptBuilder.FitToBudget`). Doing the popup's cut with
  the same code guarantees they stop at the same character.
- The component stays a thin view (constitution: no business logic in view components).

**Alternatives considered**:
- *Cut in the browser after downloading the whole text, taking the limit from `/api/documents/ask/limits`*: needs a new limit
  field there and a second copy of the cut rule in TypeScript, and downloads the whole text. Rejected.
- *A fixed limit in the browser*: contradicts FR-008 (it would drift from the model's limit). Rejected.

## R2. Shape of the endpoint

**Decision**: A new endpoint `GET /api/documents/{id}/text/preview`, next to the existing `GET /api/documents/{id}/text`, which is
left unchanged. Response: `{ documentId, content, truncated, totalCharacters }`. Errors as the existing endpoints: `404` for an
unknown id, `409` with `{ "error": "Document is not ready. Current status: <status>." }` when the document is not ready.

**Rationale**: `/text` is the contract of feature 001 ("the extracted text is retrievable ... independently of the original file") and
returns the whole text; the quickstarts of features 001 and 003 use it that way. A separate endpoint keeps that contract intact and
names what the new one is: an excerpt. The same `404`/`409` pair keeps the browser's error handling uniform.

**Alternatives considered**:
- *A query parameter on `/text` (`?preview=true`)*: one URL with two different response shapes depending on a flag. Rejected.
- *Changing `/text` to always cut*: breaks the full-text contract and the manual checks that read a whole text. Rejected.
- *`totalCharacters` only when truncated*: an optional field makes the client branch for nothing; it is always present. Rejected.

## R3. Sharing the cut with the question flow

**Decision**: Move the cut out of `QuestionPromptBuilder` into a small Core class `TextExcerpt` (`TextExcerpt.Of(text, maxCharacters)`
returning the text, whether it was cut, and the original length). `QuestionPromptBuilder` and the new service both use it.

**Rationale**: The existing cut is careful: it never ends on half of a character (a high surrogate at the cut point is dropped).
Copying it would leave two rules that can drift; the spec's assumption is that the popup stops exactly where the model's reading stops.
`QuestionPromptBuilder`'s behaviour does not change, and its existing tests stay as they are and prove it.

**Alternatives considered**: *Duplicate the five lines in the new service*: the drift risk above. Rejected. *Make the new service call
`QuestionPromptBuilder`*: the builder builds prompts and knows about instructions and history; the preview has no business with them. Rejected.

## R4. What counts as "characters"

**Decision**: The characters are .NET `string` code units (UTF-16), the same unit the question flow uses. `totalCharacters` is the length
of the stored text; the number shown is the length of the returned `content`, which the browser reads as `content.length` (JavaScript strings
are also UTF-16, so the two agree).

**Rationale**: One unit end to end, equal to what feature 002 cuts. A cut that would split a surrogate pair is moved one unit back, so the
returned content can be one unit shorter than the limit; the notice then says the real number shown.

## R5. How the popup is built

**Decision**: A component with the browser's native `<dialog>` element opened with `showModal()`. Escape, the focus trap, making the
page behind inert and giving the focus back to the button that opened it come from the browser. A click on the backdrop closes it (a
small click handler on the dialog); a Close button is the explicit control. Page scrolling behind it is blocked with a style rule on the
`body` when a dialog is open.

**Rationale**: FR-004 and FR-011 (three ways to close, keyboard only, focus returns to the row) are exactly what a modal `<dialog>` provides, and
it is supported by every current browser the app targets. It adds no dependency.

**Alternatives considered**:
- *A hand-made overlay `div`*: would need our own focus trap, Escape handling, `aria-modal` and focus return: more code and more bugs.
  Rejected.
- *Angular CDK dialog or Angular Material*: a new npm package for something the platform already does. Rejected (no new packages without asking).

## R6. Which state says what is open

**Decision**: `TextPopupSelectionService` holds the open document as a signal (`open(document)`, `close()`), the same pattern as
`ConversationSelectionService`; the list calls `open`, the popup component reads it. The component loads the preview with a `switchMap`
on the selected document, so a second selection cancels the first request and the popup can never show the text of a previous document.

**Rationale**: The list and the popup are siblings in the app shell, exactly like the list and the question panel. Keeping the popup's
state apart from the conversation's state satisfies "opening the popup does not change the question panel" (FR-010) by construction.

**Alternatives considered**: *Put the dialog inside the list component*: the list would grow (loading, error and text states) and mix two
concerns. Rejected. *Reuse `ConversationSelectionService`*: it would make opening the popup change the question panel. Rejected.

## R7. Showing the text safely

**Decision**: The text is bound with interpolation (`{{ ... }}`) inside a `<pre>`-style element with `white-space: pre-wrap` and a
scrollable area. Never `innerHTML`.

**Rationale**: FR-005. Angular escapes interpolated text, so `<script>` or `<b>` appear as characters; `pre-wrap` keeps the line breaks and
wraps long lines inside the popup. The text is selectable and copyable for free (read-only means no editable control is used).

## R8. Loading and failure

**Decision**: The popup opens at once, shows "Loading…" while the request runs and, if it fails, a message "The text is not available." (a `404`
or `409` gets the server's wording when there is one). The Close button works in every state. There is no automatic retry.

**Rationale**: FR-009. A document deleted or not ready any more between the list refresh and the click answers `404`/`409`, which the popup
reports; the list itself reloads on its own schedule.

## R9. The button

**Decision**: A secondary button labelled **Text**, with the accessible name "View extracted text of <file name>", in the actions cell, shown only
for `ready` documents, placed before **Ask**.

**Rationale**: FR-001 and the existing pattern of the **Ask** button (same visibility rule, same styling class). No need to hide it while another
action is pending.
