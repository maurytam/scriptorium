# Feature Specification: Extracted-Text Popup

**Feature Branch**: `005-extracted-text-popup`

**Created**: 2026-11-10

**Status**: Draft

**Input**: User description: "Extracted-text panel in the document list. Today the Angular UI never shows the text that Scriptorium extracts from a document; it is only readable through GET /api/documents/{id}/text. Add a way to see that text from the document list: a popup (modal dialog) opened from a document row, read-only, that shows the extracted text of a ready document (for example the digital-signature note at the start of a signed PDF). No search inside the text for now. When the text is long, the popup shows it truncated (cut), and says it is truncated. Only documents in the ready state have text to show."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the extracted text of a document (Priority: P1)

The user has uploaded a document and it is ready. They want to see exactly the text Scriptorium extracted from it, without leaving the document list. From the row of a ready document they open a popup that shows that text, read it, and close the popup. The popup only shows the text: nothing in it can be changed.

This is how the user can check, for example, that a signed PDF starts with the note "file firmato con firma digitale da ...", or understand why an answer about a document was wrong by looking at what the model was given to read.

**Why this priority**: Today the extracted text cannot be seen anywhere in the app. Seeing it is the whole point of the feature, and it works on its own even before long texts are handled specially.

**Independent Test**: Upload a short text file and a signed PDF, wait until both are ready, open the popup of each, and check that the text shown is the text of the document (for the signed PDF, starting with the signature note). Close the popup and check the list is as it was.

**Acceptance Scenarios**:

1. **Given** a document in the list whose status is ready, **When** the user chooses to view its text, **Then** a popup opens over the list showing the document's name and its extracted text, with the line breaks of the text preserved.
2. **Given** the popup is open, **When** the user closes it (with its close button, with the Escape key, or by clicking outside it), **Then** the popup disappears and the list is shown again unchanged.
3. **Given** a signed PDF that is ready, **When** the user opens its popup, **Then** the text starts with the digital-signature note, followed by a blank line and the page text.
4. **Given** the popup is open, **When** the user tries to edit the text, **Then** nothing changes: the text is read-only (it can still be selected and copied).
5. **Given** a document whose status is processing or failed, **When** the user looks at its row, **Then** no way to view extracted text is offered, because there is no text to show.

---

### User Story 2 - Long texts are shown cut, and the popup says so (Priority: P2)

Some documents have a very long text. The popup shows only the beginning of such a text, and tells the user clearly that it is cut and how much of it is shown, so the user never believes they are reading the whole text when they are not.

**Why this priority**: Without it, a very long document could make the popup slow or unreadable. It matters less than the basic view because most documents are short, and the beginning of a document is where the most useful information (such as the signature note) is.

**Independent Test**: Upload a document with a text longer than the limit and one shorter than the limit. Open the popup of the long one and check that it shows only the beginning and states that the text is cut; open the short one and check that it is shown in full with no such statement.

**Acceptance Scenarios**:

1. **Given** a ready document whose extracted text is longer than the display limit, **When** the user opens its popup, **Then** only the beginning of the text is shown, and the popup states that the text is cut, saying how many characters are shown out of the total.
2. **Given** a ready document whose extracted text is not longer than the display limit, **When** the user opens its popup, **Then** the whole text is shown and the popup does not say anything about cutting.
3. **Given** a long text that is shown cut, **When** the user compares it with the full text, **Then** what is shown is exactly the first characters of it, with nothing added, removed or reordered.

---

### Edge Cases

- The text is still being loaded when the popup opens: the popup shows that it is loading, and the user can close it at any time.
- The text cannot be loaded (the server does not answer, or the document was deleted or is no longer ready in the meantime): the popup shows a clear message that the text is not available, and the user can close it. The list is not affected.
- The text contains characters that look like markup (for example `<b>` or `<script>`): they are shown as plain characters, never interpreted. The same holds for a signer name taken from a file.
- The text has very long lines, many lines, or both: the popup stays inside the window and scrolls, and the rest of the page does not scroll behind it while it is open.
- The user opens the popup of one document, closes it, and opens another: each popup shows the text of its own document, never that of the previous one.
- The question panel (feature 002) is open for a document: opening and closing the text popup does not change the conversation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The document list MUST offer, on the row of each document whose status is ready, an action to view the extracted text of that document. It MUST NOT offer it for documents that are processing or failed.
- **FR-002**: Choosing that action MUST open a popup over the list (a modal dialog) showing the name of the document and its extracted text, as plain text, with the line breaks of the text preserved.
- **FR-003**: The popup MUST be read-only: the user cannot change the text, and opening or closing the popup MUST NOT change any document, any stored text or any other data. The text MUST remain selectable and copyable.
- **FR-004**: The popup MUST be closable with an explicit close control, with the Escape key and by clicking outside it. While it is open, the list behind it MUST NOT be usable.
- **FR-005**: The text MUST be shown exactly as it was extracted, including the digital-signature note of a signed PDF at its start (feature 003), without adding, removing or reordering anything. Characters that look like markup MUST be shown as plain text, never interpreted.
- **FR-006**: When the extracted text is longer than the display limit, the popup MUST show only its first characters up to the limit, and MUST state that the text is cut and how many characters are shown out of the total.
- **FR-007**: When the extracted text is not longer than the display limit, the popup MUST show the whole text and MUST NOT state anything about cutting.
- **FR-008**: The display limit MUST be the same as the number of characters of a document that the model reads when the user asks a question about it (8,000 characters by default, feature 002), so that the popup shows what the model is given to read. If that limit changes, the popup's limit changes with it.

- **FR-009**: While the text is being loaded the popup MUST show that it is loading, and if the text cannot be loaded it MUST show a clear message that the text is not available. In both cases the user MUST be able to close the popup.
- **FR-010**: Opening the popup MUST NOT change what is stored for the document, what the model reads when the user asks a question about it, or the state of an open question panel.
- **FR-011**: The popup MUST be usable with the keyboard alone (open from the row, move inside it, close it) and, when it closes, the user MUST find themselves back on the row they opened it from.

### Key Entities *(include if feature involves data)*

- **Extracted text**: The plain text Scriptorium extracted from a ready document (feature 001), including, for a signed PDF, the digital-signature note at its start (feature 003). It exists only for documents in the ready state. This feature only reads it.
- **Display limit**: The maximum number of characters of an extracted text that the popup shows: the same limit the model reads when answering questions (8,000 by default). Beyond it, the text is cut at the end.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From the document list, the user can see the extracted text of a ready document with a single action on its row, and the text is visible within 2 seconds for any document whose text is within the display limit.
- **SC-002**: For 100% of the signed test PDFs, the text shown in the popup starts with the digital-signature note exactly as in the extracted text.
- **SC-003**: For 100% of the documents longer than the display limit the popup states that the text is cut and how many characters are shown out of the total; for 100% of the documents within the limit it states nothing about cutting.
- **SC-004**: The popup can be closed in each of the three ways (close control, Escape key, click outside), and after closing it the list and every other action on it behave exactly as before the feature (asking questions, deleting, uploading).
- **SC-005**: No document, stored text or conversation is changed by opening and closing the popup, over any sequence of openings.

## Assumptions

- The user is a single local user; there is no permission concept. Every ready document can be viewed.
- The popup shows text only: no search inside it for now (decided by the user), no editing, no download, no formatting of the text.
- The text is cut at the end, so the beginning is always kept. This is where the signature note of a signed PDF is.
- The limit counts characters of the extracted text and is the one already used when asking questions (feature 002), chosen by the user so that the popup shows what the model reads. A document longer than the limit therefore appears cut in the popup exactly where the model's reading stops.
- The extracted text is already available from the existing service for ready documents (feature 001); this feature adds no new stored data and needs no schema change.
- Documents uploaded before the digital-signature feature show the text they already had; they are not rewritten.
- The popup follows the look and the light/dark theme of the rest of the app and works in the same browsers the app already supports on macOS.
