# Quickstart: Validating the Extracted-Text Popup

## Prerequisites

- .NET 10 SDK and Node with the Angular CLI, repository built: `dotnet restore && dotnet build`, and in `src/Scriptorium.Web`: `npm install`
- Automated tests: `dotnet test` and, in `src/Scriptorium.Web`, `npm test` (they need no files)
- For the manual scenarios: the API running against a throwaway database and folder on its own port (not the one used for daily work), and
  the Angular app opened in a browser:

  ```bash
  ASPNETCORE_URLS=http://localhost:5099 \
  ConnectionStrings__Default="Data Source=/tmp/quickstart/t.db" \
  Storage__LocalPath=/tmp/quickstart/docs \
  dotnet run --no-launch-profile --project src/Scriptorium.API
  ```

- Fake files for the scenarios, **different content each** (identical content is rejected as a duplicate): a short `.txt`, a long `.txt`
  longer than 8,000 characters, a signed PDF (for example `signed-with-name.pdf` from feature 003's manual checks) and a document whose
  text contains `<b>bold</b> <script>alert(1)</script>`. **No real user document is used.**

## Scenario 1: Read a short text (User Story 1)

Upload the short `.txt`, wait until it is *ready*, click **Text** on its row.

**Expected**: a popup opens with the title `Extracted text of <file name>` and the whole text, line breaks as in the file. There is no
notice about cutting. It appears within 2 seconds.

## Scenario 2: Close it three ways

Open the popup and close it with the **Close** button; open it again and press **Escape**; open it again and click outside it.

**Expected**: each time the popup disappears, the list is as before, and the focus is back on the **Text** button of that row.

Then repeat once with the keyboard only: Tab to the **Text** button, Enter to open, Tab inside the popup, Escape to close.

**Expected**: everything is reachable and usable without the mouse, and the focus ends on the same **Text** button.

## Scenario 3: Signed PDF (User Story 1)

Open the **Text** of the signed PDF.

**Expected**: the text starts with `file firmato con firma digitale da <name> — <date> <time> <zone>`, then a blank line, then the page text.

## Scenario 4: Only ready documents have the button (User Story 1)

Upload a large file and look at its row while it is *processing*; upload a scanned/image-only PDF, which ends *failed*.

**Expected**: no **Text** button on the processing row or on the failed row; it appears on the first as soon as it is *ready*.

## Scenario 5: Read-only and plain text

In the popup of the document containing `<b>bold</b> <script>alert(1)</script>`, try to type in the text; select and copy part of it.

**Expected**: the characters `<b>bold</b> <script>alert(1)</script>` are shown literally (no bold, no alert); typing changes nothing; copying works.
Closing and reopening shows the same text.

## Scenario 6: Long text is cut and says so (User Story 2)

Open the **Text** of the long `.txt`.

**Expected**: only the beginning is shown, with a note `Showing the first 8,000 of <total> characters.` (the total is the real length of
the text; if the cut falls on half of a character the shown number is one less). The text shown is exactly the first characters of the file.
Opening a short document afterwards shows no note. Also check: a very long line wraps inside the popup instead of widening it, the text scrolls inside the popup, and the page behind does not scroll while the popup is open.

## Scenario 7: Same cut as the model reads (User Story 2)

Ask the long document a question whose answer is in the last part of the file, and compare with the popup.

**Expected**: the answer says the document is cut (feature 002's notice) and the popup stops at the same place where the model's reading stops.

## Scenario 8: Failure and loading

Open the popup of a ready document, then (in another tab or with `curl -X DELETE http://localhost:5099/api/documents/{id}`) delete that document
before clicking **Text** on the already displayed row.

**Expected**: the popup says `The text is not available.` and can be closed; the list is not broken.

## Scenario 9: Nothing changes (User Story 1)

Open and close the popup of several documents in a row, then ask a question in the question panel and delete a test document.

**Expected**: documents, texts and the conversation in the question panel are exactly as before; **Ask** and **Delete** work as usual.

## Scenario 10: No regression

```bash
dotnet test
cd src/Scriptorium.Web && npm test
```

**Expected**: all existing tests still pass (feature 001 to 003 included), and the new ones pass.

## Scenario 11: The endpoint

```bash
curl http://localhost:5099/api/documents/{id}/text/preview
curl -i http://localhost:5099/api/documents/00000000-0000-0000-0000-000000000000/text/preview
```

**Expected**: `200` with `content`, `truncated` and `totalCharacters` for a ready document (and `truncated: true` for the long one); `404` for the unknown id;
`409` with `{ "error": "Document is not ready. Current status: processing." }` while a document is processing. See
[contracts/text-preview-api.md](./contracts/text-preview-api.md).

Results of the last run are recorded in `validation-results.md` once the feature is implemented.
