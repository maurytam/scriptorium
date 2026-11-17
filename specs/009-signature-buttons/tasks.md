---

description: "Task list for the Signature Author and Signature Date Buttons"

---

# Tasks: Signature Author and Signature Date Buttons

**Input**: Design documents from `/specs/009-signature-buttons/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/signatures-api.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity). Backend: xUnit + FluentAssertions + Moq, plus endpoint tests on the in-process `ApiTestHost`. Frontend: Jasmine/Karma specs next to each component and service. Tests are written **first** and must be seen **failing** (or, where a case is already covered by earlier work, seen passing and said so) before the code that satisfies them.

**Organization**: Tasks are grouped by user story (from spec.md). The shared format of the signature line is moved into Core in Foundational, with **no change of behaviour**: `PdfSignatureNoteReaderTests`, `SignerNameReaderTests`, `PdfDocumentParserTests`, `WordDocumentParserTests` and `ExcelDocumentParserTests` must keep passing **without any edit** (that is the proof). User Story 1 (Word and Excel signatures written at upload) comes first because the buttons depend on it for those types; the backend endpoint and the first button come in User Story 2, the second button in User Story 3.

**Commits**: Nothing is committed until the user has tried the stories with the project's manual checks and given the OK (project rule). Work stays on branch `013-signature-buttons`. There is **one** commit for the whole feature and **one** pull request, opened only after the user's OK (T052).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1 to US5)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Core/`, `src/Scriptorium.Infrastructure/Parsing/`, `src/Scriptorium.API/`, `src/Scriptorium.Web/src/app/documents/`, `tests/Scriptorium.Core.Tests/`, `tests/Scriptorium.Infrastructure.Tests/` (it also holds the endpoint tests). Manual-test files go in `~/Desktop/prove-scriptorium` (fake files only, never a real document). The tool that builds the fake signed Word and Excel files is a throwaway program in the scratchpad, **not** part of the project.

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" can be checked at the end, and have the fake files

- [X] T001 Check that the current branch is `013-signature-buttons`, then run `dotnet build` and `dotnet test` and, in `src/Scriptorium.Web`, `npm test -- --watch=false --browsers=ChromeHeadless`, before any change; note the build warnings and the number of passing tests per test project (backend: Core and Infrastructure; frontend: Karma). Expected: Core 535, Infrastructure 317, frontend 206, 0 warnings. If the frontend tests cannot run, say so to the user before going on (reference for T033, T051)
- [X] T002 [P] Build the fake files for the manual checks with a throwaway program kept **outside the project** (start from the spike in the scratchpad folder `sigspike`): in `~/Desktop/prove-scriptorium`, `signed-one.docx` (one signature, a name, a date with a zone), `signed-two.xlsx` (two signatures, the second with no date), `signed-xades-time.docx` (only the XAdES `SigningTime`), `signed-no-signer.xlsx` (no certificate), `signed-no-text.docx` (signature, no body text), `unsigned.docx`, `unsigned.xlsx` and `quoted-wording.txt` (a text file whose first lines are in the note format). The signatures are made with a self-signed certificate of an **invented name**; no real person, no real document, no key. The signed and unsigned PDFs (`signed-one.pdf`, `signed-two.pdf`, `signed-no-date.pdf`, `unsigned-one.pdf`...) already exist there

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Put the line format in one place in Core and share the name cleaning, so the PDF reader, the new Office reader and the buttons agree. **Nothing here changes what the user sees**, and every existing test keeps its assertions and its set-up.

**⚠️ CRITICAL**: No story work can start until this is complete

### Tests for the shared pieces (write first, they must fail) ⚠️

- [X] T003 [P] Create `tests/Scriptorium.Core.Tests/Services/SignatureNoteTests.cs` for the static class `SignatureNote` (no test needs a model or a file): `Line(signer, signedAt)` gives the five layouts of `data-model.md` (name and date with zone, name and date, name only, unknown signer with date, unknown signer only); `FormatDate(date, showZone)` gives `yyyy-MM-dd HH:mm:ss` with or without ` +01:00`; `ReadLeading(text)` reads one line, several lines, lines followed by a blank line and text, lines that are the whole text, Windows line ends, a name that contains ` — `, a name that contains digits that look like a date, the unknown signer, a line with no date; it gives **nothing** when the wording is not on the first line, when the wording appears further down, when a matching line is not followed by a blank line or the end of the text, and for empty text; `CanBeSigned(fileType)` is true for `pdf`, `docx` and `xlsx` in any letter case and false for `txt` and anything else; and `ReadLeading(Line(...))` returns what was written for every layout (round trip)
- [X] T004 [P] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/SignerNameTextTests.cs` for `SignerNameText.Clean` with the cases that `SignerNameReader` already has today (a null or blank name gives null; control characters and runs of white space become one space; the text is cut at 100 characters without leaving half of a surrogate pair)

### Implementation for the foundation

- [X] T005 Create `src/Scriptorium.Core/Results/SignatureEntry.cs` (a record with `string? Signer` and `string? SignedAt`) and `src/Scriptorium.Core/Services/SignatureNote.cs`: the one definition of the wording `file firmato con firma digitale`, `da`, `(firmatario sconosciuto)`, ` — ` and the date layout, with `Line`, `FormatDate`, `ReadLeading` (returns `IReadOnlyList<SignatureEntry>`) and `CanBeSigned`, each method under 30 lines; T003 passes
- [X] T006 Create `src/Scriptorium.Infrastructure/Parsing/SignerNameText.cs` by moving `Clean`, `Cut` and `AppendWithPendingSpace` out of `SignerNameReader` (internal static, same code); change `src/Scriptorium.Infrastructure/Parsing/SignerNameReader.cs` to call it, so it behaves exactly as before; T004 passes
- [X] T007 Change `src/Scriptorium.Infrastructure/Parsing/PdfSignatureNoteReader.cs` to build its line with `SignatureNote.Line` and its date text with `SignatureNote.FormatDate`, and delete its private wording constants and `Format`; the text it produces must not change by one character
- [X] T008 [P] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/SignatureNoteRoundTripTests.cs`: for the PDFs built by `SignedPdfFixture` (one signature with a name and a date with a zone; no zone; no date; unknown signer; two signatures in order), the text of `PdfDocumentParser` read with `SignatureNote.ReadLeading` gives the signer and the date that were put in; an unsigned PDF gives nothing. This is the proof that the writer and the reader agree
- [X] T009 Run `dotnet build` and `dotnet test`: T003, T004 and T008 pass; **every existing test passes without any edit**; `git status` and `git diff main --stat -- tests` show only new test files and no change to `PdfSignatureNoteReaderTests.cs`, `SignerNameReaderTests.cs` or `PdfDocumentParserTests.cs`; no new build warning

**Checkpoint**: The signed PDFs behave exactly as before and the shared format and name cleaning are available

---

## Phase 3: User Story 1 - Signed Word and Excel files are recognised like signed PDFs (Priority: P1) 🎯 MVP

**Goal**: At upload, a signed `.docx` or `.xlsx` gets one line per signature at the start of its text, in the format already used for PDFs; an unsigned one gives the text it gave before.

**Independent Test**: Upload `signed-one.docx`, `signed-two.xlsx`, `unsigned.docx` and `unsigned.xlsx` and open **Text**: the signed ones start with the lines, the unsigned ones are unchanged.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [X] - [ ] T010 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/SignedOfficeFixture.cs` (a helper, not a test class): builds a `docx` or an `xlsx` in memory with a body text and any number of `FakeOfficeSignature` (signer name used as the certificate's subject, or none; Office `SignatureTime` text or none; XAdES `SigningTime` text or none; a flag for a certificate that is not a certificate; a flag for a signature part whose XML is broken), using the library's `DigitalSignatureOriginPart` and `XmlSignaturePart` and a self-signed certificate made with `CertificateRequest` for an **invented name** (the spike did exactly this)
- [X] - [ ] T011 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/OfficeSignatureNoteReaderTests.cs` for `OfficeSignatureNoteReader` on Word and on Excel files from T010: one signature with a name and a date with an offset gives the line with the offset; `Z` gives `+00:00`; a date with no zone gives no zone; a date with only a day or only a day and an hour gives no date; two signatures keep the order of the file; no certificate and a certificate that cannot be read give the unknown signer; `SignatureTime` missing but XAdES `SigningTime` present gives the date; both present uses `SignatureTime`; a date that is not ISO gives no date; a signature part with broken XML gives no line while the other signature is still read; no signature at all gives nothing; a package that is not a valid Office file gives nothing and does not throw; a cancelled token is not swallowed; the name is one line of at most 100 characters, also for a name with line breaks and a sentence in the style of an instruction to a model (it comes out as one clean line); a signature with a chain of certificates gives the **signer's** name, not the authority's, whatever the order (authority first, signer first, an intermediate authority between, a single certificate, and only authority certificates as the fallback to the first); a signature part with a `DOCTYPE` or entities gives no line and does not throw
- [X] - [ ] T012 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/SignedOfficeDocumentParserTests.cs` for the decorator: with a strict mock inner `IDocumentParser` and a strict mock `IOfficeSignatureNoteReader`: `SupportedFileType` is the inner one; an inner failure is returned as it is and the reader is never called; with no lines the inner text comes back unchanged; with lines the result is the lines, a blank line and the text; with lines and an empty text it is only the lines; the stream is put back at its start before the signatures are read; a stream that cannot be put back gives the inner text unchanged, with no line and no exception. With the real parsers and the real reader on the T010 files: a signed Word and a signed Excel start with their lines; an unsigned Word and an unsigned Excel give exactly the text of the plain parser; a signed Word with no body text is a success with only the line

### Implementation for User Story 1

- [X] - [ ] T013 [US1] Create `src/Scriptorium.Infrastructure/Parsing/IOfficeSignatureNoteReader.cs`: `IReadOnlyList<string> Read(Stream content, string fileType)` (interface first)
- [X] - [ ] T014 [US1] Create `src/Scriptorium.Infrastructure/Parsing/OfficeSignatureNoteReader.cs` implementing it, every method under 30 lines: open the package for `docx` or `xlsx`, take `DigitalSignatureOriginPart.XmlSignatureParts` in the order given, read each with `System.Xml.Linq`, read the XML with DTD processing prohibited and no resolver; take the signer from the **signer's own certificate** among the `X509Certificate` entries (the first, in file order, that is not a certification authority and did not issue another certificate of the same signature; the first one if none passes) with `X509CertificateLoader` and `SignerNameText.Clean` (unknown when missing or unreadable); dispose every certificate loaded, take the date from `SignatureTime/Value` and, if missing, from `SigningTime` (by local name), accept only an ISO date and time with at least hour and minute and show the zone only if declared, build the line with `SignatureNote.Line`; catch every exception except cancellation and return no lines; T011 passes
- [X] - [ ] T015 [US1] Create `src/Scriptorium.Infrastructure/Parsing/SignedOfficeDocumentParser.cs`: a sealed `IDocumentParser` that wraps an inner parser and an `IOfficeSignatureNoteReader` (constructor injection), asks the inner parser for the text, and on success rewinds the stream and puts the lines in front (`lines`, a blank line, the text; only the lines when the text is empty); an inner failure passes through untouched; a stream that cannot be rewound gives the inner text unchanged; T012 passes
- [X] - [ ] T016 [US1] In `src/Scriptorium.API/Program.cs` register `IOfficeSignatureNoteReader` as a singleton and replace the registrations of `WordDocumentParser` and `ExcelDocumentParser` by `SignedOfficeDocumentParser` wrapping each (the inner parsers are created in the registration, not looked up); check that `DocumentProcessingQueue` still finds one parser per file type
- [X] - [ ] T017 [US1] Run `dotnet build` and `dotnet test`: T010 to T012 pass, **`WordDocumentParserTests` and `ExcelDocumentParserTests` and every other existing test pass without any edit**, no new build warning
- [X] T018 [US1] With the user: start the throwaway API on port 5099 (cloud off, empty key, a temporary database and folder in the scratchpad) and a separate page on 4299 (never the user's 5034 and 4200); upload `signed-one.docx`, `signed-two.xlsx`, `signed-xades-time.docx`, `signed-no-signer.xlsx`, `signed-no-text.docx`, `unsigned.docx` and `unsigned.xlsx` (check `isPrivate` in the list after each upload); open **Text** for each and check quickstart scenarios 1 and 8. **Wait for the user's OK before anything is committed**; record the outcome in `specs/009-signature-buttons/validation-results.md` (create it)

**Checkpoint**: User Story 1 works on its own: signed Word and Excel files show their signatures in the text

---

## Phase 4: User Story 2 - See who signed a document with one click (Priority: P1)

**Goal**: **Signature author** shows the signers of a ready PDF, Word or Excel file, with no model.

**Independent Test**: With the signed fake files, press **Signature author** and check the names appear at once, also with Ollama off and Cloud chosen.

### Tests for User Story 2 (write first, they must fail) ⚠️

- [X] T019 [P] [US2] Add to `tests/Scriptorium.Core.Tests/Services/ReadyDocumentTextTests.cs` (new tests only, no existing line changed): a loaded document exposes its `FileType`; a failed load has none
- [X] T020 [P] [US2] Create `tests/Scriptorium.Core.Tests/Results/SignatureResultTests.cs`: `Success(entries)` is a success with its entries in order and no failure kind; `Failure(kind, error)` is not a success, keeps the kind and the message and has an empty (never null) list
- [X] T021 [P] [US2] Create `tests/Scriptorium.Core.Tests/Dtos/SignatureDtosTests.cs`: `SignaturesResponseDto.From(result)` maps each entry to `signer` and `signedAt` in order, keeps a null signer and a null date as null, and gives an empty list for no signature
- [X] T022 [P] [US2] Create `tests/Scriptorium.Core.Tests/Services/DocumentSignatureServiceTests.cs` with a strict mock of `IDocumentRepository` as the only collaborator: not found gives `NotFound` with `Document not found.`; not ready gives `NotReady`; a ready document with no stored text gives `Internal`; a `pdf`, a `docx` and an `xlsx` whose text starts with one line and with two lines give the entries in order; the type is matched in any letter case; a `txt` whose text starts with note lines gives no entries; a `pdf` with the wording only further down gives none; a very long text (over 8,000 characters) with the lines at its start still gives them; the service does not need a router, a scorer or a provider (the constructor takes only the repository)
- [X] T023 [P] [US2] Create `tests/Scriptorium.Infrastructure.Tests/Endpoints/SignatureEndpointTests.cs` on `ApiTestHost` with strict stand-ins for the model providers that fail the test if they are called: `GET /api/documents/{id}/signatures` on an uploaded signed PDF (from `SignedPdfFixture`), on a signed `docx` and a signed `xlsx` (from T010) returns `200` with the entries in the order of the file; an unknown id gives `404` with `{ "error": "Document not found." }`; a document still processing gives `409` with the not-ready message; the response keeps `null` for a missing signer or date; with the cloud switched on or off and with no model server the answer is the same
- [X] T024 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/document-api.service.spec.ts` (new tests only): `getSignatures(id)` sends `GET /api/documents/{id}/signatures` and returns the body
- [X] T025 [P] [US2] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (new tests only; the only change to its set-up is `'getSignatures'` in the list of methods of the API spy): the **Signature author** button exists next to **Summarise** and **Extract entities**; pressing it calls `getSignatures` with the open document and **no preference**, and shows a card headed **Signature author** with one line per signer in order; an unknown signer reads `Unknown signer`; the note `The name and date are what the file declares. The signature has not been validated.` is shown with signatures; the buttons are disabled while it runs; a failure shows the alert with the message of the failure and no card; the **Answer with** choice does not change the call

### Implementation for User Story 2

- [X] T026 [US2] Add `FileType` to `src/Scriptorium.Core/Services/ReadyDocumentText.cs` (set from the loaded document, empty on failure); T019 passes and no other behaviour changes
- [X] T027 [P] [US2] Create `src/Scriptorium.Core/Results/SignatureResult.cs` (its own typed result: entries, never null; a failure kind and message from `AskFailureKind`); T020 passes
- [X] T028 [P] [US2] Create `src/Scriptorium.Core/Dtos/SignatureDtos.cs`: `SignatureDto(string? Signer, string? SignedAt)` and `SignaturesResponseDto(IReadOnlyList<SignatureDto> Signatures)` with `From(SignatureResult)`; T021 passes
- [X] T029 [US2] Create `src/Scriptorium.Core/Services/DocumentSignatureService.cs` with `ReadAsync(Guid, CancellationToken)`: `ReadyDocumentText.LoadAsync`, then `SignatureNote.CanBeSigned(fileType)` and `SignatureNote.ReadLeading(text)`; only `IDocumentRepository` is injected; T022 passes
- [X] T030 [US2] Create `src/Scriptorium.API/Endpoints/SignatureEndpoints.cs` (`GET /api/documents/{id:guid}/signatures`, success `Ok(SignaturesResponseDto.From(...))`, failure `AskFailureResponses.From`) and, in `src/Scriptorium.API/Program.cs`, register `DocumentSignatureService` and call `app.MapSignatureEndpoints()`; T023 passes
- [X] T031 [P] [US2] Add `SignatureEntry` and `SignaturesResponse` to `src/Scriptorium.Web/src/app/documents/document.models.ts` and `getSignatures(id)` to `src/Scriptorium.Web/src/app/documents/document-api.service.ts`; T024 passes
- [X] T032 [US2] In `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html` and `src/Scriptorium.Web/src/styles.css`: a **Signature author** button next to the other two, a `signatureAuthor` signal, a card headed **Signature author** with one line per signer, `Unknown signer`, and the not-validated note; it goes through the same private `show` helper as the other cards; `reset()` clears it and **New conversation** shows when it is on screen; each method under 30 lines; T025 passes
- [X] T033 [US2] Run `dotnet build`, `dotnet test`, and in `src/Scriptorium.Web` `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`: T019 to T025 pass, no existing test fails or is edited, no new warning; compare with the T001 baseline
- [X] T034 [US2] With the user: restart my throwaway API (5099) and page (4299); check quickstart scenarios 2, 4 and 7 (the names of the signed PDF, Word and Excel in order; `Unknown signer`; Ollama switched off and **Cloud** chosen: still answers; the endpoint with curl for a known id, an unknown id and a document still processing; the time of the call, which must be well under 1 second). **Wait for the user's OK**; record the outcome in `validation-results.md`

**Checkpoint**: User Stories 1 and 2 work: the author of a signature is one click away for PDF, Word and Excel

---

## Phase 5: User Story 3 - See when a document was signed with one click (Priority: P1)

**Goal**: **Signature date** shows when each signature was made, with the signer when there are several, as declared.

**Independent Test**: Press **Signature date** on a document with one dated signature, one with two, and one with no date.

### Tests for User Story 3 (write first, they must fail) ⚠️

- [X] T035 [P] [US3] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (new tests only): the **Signature date** button exists next to the others; pressing it calls `getSignatures` and shows a card headed **Signature date** with the date exactly as sent (with its zone when sent with one, never converted); with several signatures each date line names its signer (and `Unknown signer` when there is none); with one signature the signer is not repeated; a missing date reads `No signing date declared in the file.`; the not-validated note is shown; buttons disabled while it runs; both cards can be on the page at the same time and each button replaces only its own card

### Implementation for User Story 3

- [X] T036 [US3] In `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.ts` and `.html` and `src/Scriptorium.Web/src/styles.css`: the **Signature date** button, a `signatureDate` signal and its card through the same `show` helper; `reset()` clears it; T035 passes
- [X] T037 [US3] Run the frontend tests and `npm run build`; all pass, no existing test edited, no warning
- [X] T038 [US3] With the user: on my throwaway servers, check quickstart scenario 3 (dates as declared, with and without a zone, two signatures each with its signer, a signature with no date, the XAdES-only date). **Wait for the user's OK**; record the outcome in `validation-results.md`

**Checkpoint**: Both buttons work for signed documents

---

## Phase 6: User Story 4 - A clear answer when there is no signature (Priority: P1)

**Goal**: Documents with no digital signature get an honest message, never a name or a date.

**Independent Test**: Press each button on `unsigned-one.pdf`, `unsigned.docx`, `unsigned.xlsx`, `quoted-wording.txt` and a document uploaded before this feature.

### Tests for User Story 4 (write first; some may already pass and are kept to pin the behaviour) ⚠️

- [X] T039 [P] [US4] Add to `tests/Scriptorium.Infrastructure.Tests/Endpoints/SignatureEndpointTests.cs` (new tests only): `200` with `"signatures": []` for an unsigned PDF, an unsigned `docx` and an unsigned `xlsx`; for a `txt` whose text starts with note lines (`quoted-wording.txt`); for a PDF whose page text has the wording only further down; and for a Word uploaded before this feature (a stored text with no lines); say in the notes of the task which of these failed first and which already passed
- [X] T040 [P] [US4] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (new tests only): with an empty list, either button shows `This document has no digital signature.`, no name, no date, no not-validated note and no alert

### Implementation for User Story 4

- [X] T041 [US4] Add what is missing so that T039 and T040 pass: the empty-list message in the two cards of `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.html`; any backend gap T039 shows
- [X] T042 [US4] Run `dotnet test`, the frontend tests and `npm run build`: all pass, no existing test edited
- [X] T043 [US4] With the user: check quickstart scenario 5 on the unsigned PDF, Word and Excel, `quoted-wording.txt` and a document with no signature lines in its text, which is what a document uploaded before this feature looks like (`unsigned.docx` is enough; the real old-upload case is pinned by the automated test of T039; never open a real document of the user). **Wait for the user's OK**; record the outcome in `validation-results.md`

**Checkpoint**: The buttons never invent a signature

---

## Phase 7: User Story 5 - The buttons fit the panel and behave like the others (Priority: P2)

**Goal**: The panel stays consistent and the cards are dropped when they should be.

**Independent Test**: Open a ready document, press the four buttons, then **New conversation**, close, and open another document.

### Tests for User Story 5 (write first; some may already pass and are kept to pin the behaviour) ⚠️

- [X] T044 [P] [US5] Extend `src/Scriptorium.Web/src/app/documents/ask/ask-panel.component.spec.ts` (new tests only): the four buttons are in the same row with the same style class; the signature cards sit above the conversation and are never part of the `history` sent with a question; **New conversation**, close and opening another document remove them; a late answer after a reset is dropped; a `404` with the API's body says the document no longer exists, a `404` without it uses the not-recognised message, an unreachable server gives the usual message; pressing the same button twice replaces only that card

### Implementation for User Story 5

- [X] T045 [US5] Adjust `ask-panel.component.ts`, `.html` and `styles.css` only where T044 fails (the shared `show` helper, `reset()`); keep every method under 30 lines
- [X] T046 [US5] Run the frontend tests and `npm run build`: all pass, no existing test edited
- [X] T047 [US5] With the user: check quickstart scenario 6 on the page (light and dark theme, narrow window, the cards, the disabled buttons, **New conversation**, another document, a deleted document, the server stopped) and ask the user whether the two buttons and their result were clear without any explanation. **Wait for the user's OK**; record the outcome in `validation-results.md`; then stop my servers by their process id (5099 and 4299), never the user's

**Checkpoint**: All five stories work

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T048 [P] Update `README.md`: a line in "What it does today" about **Signature author** and **Signature date** (PDF, Word and Excel; read from the signature the file declares, no model, nothing stored, not validated, the message when there is none); the API table gets `GET /api/documents/{id}/signatures` with a link to the contract; Word and Excel signatures now appear as a line at the start of their text, like PDFs; the known limit (an unsigned file whose first lines are exactly in the note format looks signed) and that a document uploaded earlier shows no signature until it is deleted and uploaded again; link the spec folder
- [X] T049 [P] Update `docs/architecture.md`: a new flow **4.6 Reading the signatures** (sequence: page, endpoint, `DocumentSignatureService`, `ReadyDocumentText`, `SignatureNote`; no model); the upload flow 4.1 says that Word and Excel signatures are written by `SignedOfficeDocumentParser` and `OfficeSignatureNoteReader`; `SignatureNote`, `DocumentSignatureService`, `SignatureResult` and the Office classes in the layers table; the web table mentions the two buttons
- [X] T050 Review the new and changed code against the constitution: every class has one responsibility and every method is under 30 lines (`OfficeSignatureNoteReader` and `SignatureNote` in particular); no `!` without a comment, no `.Result`/`.Wait()`, no `dynamic` or reflection; **no model, router or provider type is used by the signature service or endpoint**; no `HttpClient` outside the two providers; `WordDocumentParser`, `ExcelDocumentParser` and their tests are untouched; run `git grep -nIE 'sk-ant-|ApiKey' -- ':!specs' ':!docs' ':!*.md'`: only code that reads the key option; confirm with `git diff main --name-only` that **no `.csproj`, `package.json` or `package-lock.json` changed** and that no migration or entity was added. List any deviation and ask the user before fixing anything beyond the feature
- [X] T051 Run `dotnet build`, the full `dotnet test`, and in `src/Scriptorium.Web` the full `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`; compare with the T001 baseline (no test removed, only additions, no new warnings; the only change to an existing spec set-up is `'getSignatures'` in the API spy); run quickstart scenarios 9 and 10; complete `specs/009-signature-buttons/validation-results.md` with the results of scenarios 1 to 10, saying plainly that **no real signed file of a certification provider has been tried yet** (only fake files and the self-made certificate), that the real cloud service was never called and that no model was called by these buttons
- [X] T052 Hand over to the user: summarise the work and the validation. **Only with the user's OK**: make **one** commit (English message, with the required `Co-Authored-By` line), push `013-signature-buttons`, and open **one** pull request to `main` for the whole feature; merge it only when the user says so, then update `main` and delete the branch locally and on the remote. Never commit on `main`. Afterwards mark the idea as done in the idea box memory

---

## Dependencies & Execution Order

- **Phase 1** first. **Phase 2** blocks everything: the format in Core and the shared name cleaning.
- **User Story 1** (Phase 3) needs Phase 2. **User Story 2** (Phase 4) needs Phase 2; its endpoint tests for Word and Excel use the fixture from T010, so T010 comes first. **User Story 3** needs the card helper and the models of User Story 2. **User Story 4** and **User Story 5** need User Stories 2 and 3.
- Inside a story: tests, then interface, then implementation, then the run, then the manual check with the user.
- Tasks marked **[P]** touch different files and can be done together. Tasks that edit `ask-panel.component.spec.ts`, `ask-panel.component.ts` or `ask-panel.component.html` (T025, T032, T035, T036, T040, T041, T044, T045) are **not** parallel with each other.
- The manual checks (T018, T034, T038, T043, T047) come before any commit; the single commit is T052.

## Parallel examples

- Phase 2 tests: T003, T004 together; T008 once T005 to T007 exist.
- User Story 1 tests: T010 first, then T011 and T012 together.
- User Story 2 tests: T019 to T025 together (different files); then T027 and T028 together, then T029, T030, T031.

## Implementation strategy

1. **Foundation** (T003 to T009): no visible change, proof by the unedited tests.
2. **MVP = User Stories 1 and 2**: signed Word and Excel are recognised, and **Signature author** works. Stop and let the user try it.
3. Add **Signature date**, then the honest no-signature behaviour, then the panel consistency, one story at a time, each with its manual check.
4. Polish, review, full run, and the single commit with the user's OK.
