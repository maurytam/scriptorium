# Quickstart: Validating the Digital Signature Note

## Prerequisites

- .NET 10 SDK, repository built: `dotnet restore && dotnet build`
- Automated tests: `dotnet test` (they build their own fake signed PDFs; no file is needed)
- For the manual scenarios: the API running against a throwaway database and folder, as in
  [feature 001's quickstart](../001-document-upload-parsing/quickstart.md):

  ```bash
  ConnectionStrings__Default="Data Source=/tmp/quickstart/t.db" \
  Storage__LocalPath=/tmp/quickstart/docs \
  dotnet run --project src/Scriptorium.API
  ```

- For scenarios 5-7 only: Ollama running with the default model (`qwen3.5:4b`), as in
  [feature 002's quickstart](../002-ask-document-questions/quickstart.md).
- Fake PDFs for the manual scenarios are produced during implementation (a throwaway generator outside the repository, the
  same technique as the tests) into `~/Desktop/prove-scriptorium`. **No real user document is used.** Each file must have
  **different content**, because identical content is rejected as a duplicate.

Results of the last run are recorded in `validation-results.md` once the feature is implemented.

## Scenario 1 — One signature with date (User Story 1)

```bash
curl -F "file=@signed-one.pdf" -F "isPrivate=false" http://localhost:5034/api/documents
# wait for status "ready", then:
curl http://localhost:5034/api/documents/{id}/text
```

**Expected**: the text starts with `file firmato con firma digitale — <date> <time> <zone>`, then a blank line, then the page
text. See [contracts/extracted-text.md](./contracts/extracted-text.md).

## Scenario 2 — Unsigned PDF is unchanged (User Story 1)

Upload one of the PDFs already used for features 001/002.

**Expected**: no note, no leading blank line; text identical to what it was before the feature.

## Scenario 3 — Two signatures (User Story 2)

Upload `signed-two.pdf`.

**Expected**: two note lines, each with its own date and time, in file order, then the page text.

## Scenario 4 — Edge cases (User Story 2)

| File | Expected |
|---|---|
| `signed-no-date.pdf` (signature without a date) | One note with no date; upload succeeds |
| `signed-bad-date.pdf` (unreadable date) | One note with no date; upload succeeds |
| `signature-placeholder.pdf` (empty signature field) | No note |
| `signed-scan.pdf` (signed, no page text) | Upload fails with `No extractable text — scanned/image-only PDF not supported` |
| An encrypted or corrupted PDF | Fails with the same reason as before the feature |

## Scenario 5 — Ask whether it is signed (User Story 1, needs Ollama)

In the browser, open `signed-one.pdf` and ask: *"Is this document digitally signed?"*

**Expected**: the answer says it is signed (SC-003). Ask it 3 times in a row (a new conversation each time) and all 3 answers must say so. Repeat the 3 runs with the Italian question *"Il documento è firmato digitalmente?"*.

## Scenario 6 — Ask when it was signed (needs Ollama)

Ask: *"When was it signed?"*

**Expected**: the answer gives the date and time of the note, in all of 3 runs in a row (a new conversation each time).

## Scenario 7 — Unsigned document (needs Ollama)

Open the unsigned PDF and ask: *"Is this document digitally signed?"*

**Expected**: the model says the document does not say so or does not contain it; it must not claim it is signed, in any of 3 runs in a row (a new conversation each time).

## Scenario 8 — No regression

```bash
dotnet test
```

**Expected**: all existing tests pass (SC-004), including the PDF parser tests for valid, corrupted, encrypted and image-only
files.

## Scenario 9 — Signer from the signature's name field (User Story 3)

Upload `signed-with-name.pdf` (fake: the signature has the name field "Mario Rossi", no certificate).

**Expected**: the text starts with `file firmato con firma digitale da Mario Rossi — <date> <time> <zone>`.

## Scenario 10 — Signer from the certificate only (User Story 3)

Upload `signed-with-certificate.pdf` (fake: no name field; the signature contains a fake certificate issued to "ROSSI MARIO"
after a fake authority certificate).

**Expected**: the note says `da ROSSI MARIO`, not the authority's name.

## Scenario 11 — Edge cases for the name (User Story 3)

| File | Expected |
|---|---|
| `signed-name-and-certificate.pdf` (name field "Mario Rossi", certificate "ROSSI MARIO") | `da Mario Rossi` (the name field wins) |
| `signed-certificate-unmatched.pdf` (certificates present, none matches the signer) | Note with no name; upload succeeds |
| `signed-name-blank.pdf` (blank name field plus a certificate) | The certificate's name |
| `signed-name-injection.pdf` (name field with a line break and an instruction after it) | One single line; the name is cleaned and cut at 100 characters; the extra text is not on its own line |

## Scenario 12 — Ask who signed (User Story 3, needs Ollama)

Open `signed-with-name.pdf` and ask: *"Who signed this document?"* (and in Italian *"Chi ha firmato questo documento?"*).

**Expected**: the answer gives the name, in all of 3 runs in a row (a new conversation each time) (SC-005). On the unsigned
PDF the model must not invent a signer.
