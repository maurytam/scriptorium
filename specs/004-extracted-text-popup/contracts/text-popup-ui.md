# Contract: Text popup (UI)

What the user sees and what each part guarantees. The wording is English, like the rest of the interface.

## The Text button

| Rule | Detail |
|---|---|
| Where | In the actions cell of a document row, before **Ask** |
| Label | **Text**; accessible name `View extracted text of <file name>` |
| When | Only on rows whose status is `ready`. Not on `processing` or `failed` rows |
| Effect | Opens the popup for that document |

## The popup

A modal dialog over the list, with the page behind it inert and not scrollable.

```text
┌──────────────────────────────────────────────────────┐
│ Extracted text of <file name>                [Close] │
├──────────────────────────────────────────────────────┤
│ Showing the first 8,000 of 52,310 characters.   ← only when the text is cut
│                                                      │
│ file firmato con firma digitale da Mario Rossi — …   │
│                                                      │
│ <the text, line breaks preserved, scrolls inside>    │
└──────────────────────────────────────────────────────┘
```

| Part | Detail |
|---|---|
| Title | `Extracted text of <file name>` |
| Text | Plain text, line breaks preserved, long lines wrapped, scrolls inside the popup. Never interpreted as markup. Selectable and copyable; not editable |
| Notice | Only when `truncated` is true: `Showing the first <shown> of <total> characters.` with thousands separators, as a note above the text. When the text is not cut, there is no notice |
| Loading | While the preview is requested: `Loading…` |
| Failure | `The text is not available.` (or the server's message for `404`/`409`), with the Close button still working |
| Closing | The **Close** button, the Escape key, and a click outside the popup. After closing, the focus is back on the **Text** button of the row |
| Keyboard | The Text button and the popup are reachable and usable with the keyboard alone |
| Theme | Follows the app's light/dark theme |

## What the popup never does

- It does not edit, save, download or search the text.
- It does not change a document, a stored text or the question conversation.
- It does not show a notice about cutting when the text fits.

Corresponds to FR-001 to FR-007, FR-009 and FR-011 of the spec.
