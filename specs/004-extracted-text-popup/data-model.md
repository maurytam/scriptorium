# Data Model: Extracted-Text Popup

No entity, table or column is added or changed. There is no migration. The feature reads the `ExtractedText` already stored for a
ready document (feature 001, including the digital-signature note of feature 003) and produces transient values.

## Text excerpt (Core, transient)

The result of cutting a text at a limit. Built each time a preview is requested; never stored.

| Field | Meaning | Rules |
|---|---|---|
| Text | The text, or its beginning | Equal to the original when its length is within the limit; otherwise the first characters of the original, with nothing added, removed or reordered |
| Truncated | Whether the text was cut | True only when the original is longer than the limit |
| OriginalLength | Length of the original text, in characters | Always present; equal to the length of Text when not truncated |

Rules of the cut: the limit is `Qa:MaxDocumentCharacters`; if the character at the cut point is the first half of a surrogate pair,
the cut moves one unit back so a character is never split (as `QuestionPromptBuilder` does today). A limit of 0 gives an empty text.

## Text preview result (Core)

What the use case returns. Same shape as `AskResult`.

| State | Content | When |
|---|---|---|
| Success | the excerpt (text, truncated, original length) | the document exists and is ready |
| Failure `NotFound` | an error message | no document with that id |
| Failure `NotReady` | an error message naming the status, e.g. `Document is not ready. Current status: processing.` | the document exists but is processing or failed |

## Preview DTO (API response)

| Field | Type | Meaning |
|---|---|---|
| `documentId` | guid | The document |
| `content` | string | The excerpt text |
| `truncated` | bool | True when `content` is only the beginning of the text |
| `totalCharacters` | integer | Length of the whole text; the number shown is the length of `content` |

See [contracts/text-preview-api.md](./contracts/text-preview-api.md).

## Popup state (browser only)

Lives in the Angular app, is lost on reload, and is never sent to the server.

| State | Meaning |
|---|---|
| Selected document | The list row whose text is open (`null` when the popup is closed). Set by the Text button, cleared by closing |
| Loading | A preview request is in progress for the selected document |
| Loaded | The preview of the selected document: content, truncated, total |
| Failed | The preview could not be loaded; carries the message to show |

Transitions: closed → (Text button) → loading → loaded or failed → (close) → closed. Selecting another document while one is loading
cancels the first request. Opening the popup never touches the question conversation state (feature 002).

## Which documents have a preview

| Document status | Text button | Preview request |
|---|---|---|
| ready | shown | `200` |
| processing | not shown | `409` |
| failed | not shown | `409` |
| deleted / unknown | row not shown | `404` |
