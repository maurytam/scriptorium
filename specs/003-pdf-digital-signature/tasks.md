---

description: "Task list for Digital Signature Note for Signed PDFs"

---

# Tasks: Digital Signature Note for Signed PDFs

**Input**: Design documents from `/specs/003-pdf-digital-signature/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/extracted-text.md, quickstart.md

**Tests**: Included for every production class per Constitution Principle VIII (Test Parity: xUnit + FluentAssertions + Moq). `PdfSignatureNoteReader` gets its own test class; `PdfDocumentParserTests` is extended. Tests are written before the code they cover and must fail first. The signed PDFs used by tests are fake files written by `SignedPdfFixture` as raw PDF text; no real user document is used.

**Organization**: Tasks are grouped by user story (from spec.md) so each can be implemented and tested independently. The feature is backend-only: one new class, one modified class and one DI line in the existing solution; no new package, no migration, no Angular change.

**Commits**: Nothing is committed until the user has tried the story in the browser and given the OK (project rule). Work stays on branch `004-pdf-digital-signature`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no unmet dependencies)
- **[Story]**: Which user story the task belongs to (US1, US2)
- File paths are exact, per `plan.md`'s Project Structure section

## Path Conventions

Paths per `plan.md`: `src/Scriptorium.Infrastructure/Parsing/`, `src/Scriptorium.API/`, `tests/Scriptorium.Infrastructure.Tests/Parsing/`. Manual-test PDFs go in `~/Desktop/prove-scriptorium` and are produced by a throwaway generator kept outside the repository.

---

## Phase 1: Setup

**Purpose**: Know the starting point, so "no regression" (SC-004) can be checked at the end

- [X] T001 Run `dotnet build` and `dotnet test` on branch `004-pdf-digital-signature` before any change and note the build warnings and the number of passing tests per test project (reference for T009, T027 and T032)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The test helper that both stories use to make fake signed PDFs, and the interface the parser will depend on (written before its implementation, per the constitution's workflow)

**⚠️ CRITICAL**: No story test can be written until this is complete

- [X] T002 Create the test helper `SignedPdfFixture` in `tests/Scriptorium.Infrastructure.Tests/Parsing/SignedPdfFixture.cs`: a static builder that returns the bytes of a hand-written PDF (raw text, like `EncryptedPdf` in `PdfDocumentParserTests`) with a catalog `/AcroForm`, a page with a given page text (empty text allowed, for a scan), and a list of signature specs. Each spec controls: the raw `/M` value (or none), whether the field has a `/V` signature dictionary (false = empty placeholder), and the order of the fields in `/Fields`. Include an option for a nested field (`/Kids`) and one for a malformed form (e.g. `/Fields` pointing to a missing object). Reuse the PDF layout proven in the research spike (research.md R1-R2)
- [X] T003 Create the interface `IPdfSignatureNoteReader` in `src/Scriptorium.Infrastructure/Parsing/IPdfSignatureNoteReader.cs` (in Infrastructure, not Core: it uses the PdfPig `PdfDocument` type and nothing outside Infrastructure needs it) with one member, `IReadOnlyList<string> Read(PdfDocument pdf)`, documented as returning one note per digital signature in the document's field order, an empty list when there is none or when the signatures cannot be read, and never throwing

**Checkpoint**: Fake signed PDFs can be built by any test, and the contract of the reader is fixed

---

## Phase 3: User Story 1 - See that a PDF is digitally signed (Priority: P1) 🎯 MVP

**Goal**: A PDF with a digital signature that declares a date has, at the start of its extracted text, the note `file firmato con firma digitale` plus the date and time (FR-001, FR-002, FR-005, FR-006, FR-008, FR-009). An unsigned PDF is unchanged.

**Independent Test**: Upload a PDF with one signature and read its text: the note, a blank line, then the page text. Upload an unsigned PDF: the text is identical to before. Ask the model "is it signed?" and "when?" and get a correct answer.

### Tests for User Story 1 (write first, they must fail) ⚠️

- [X] T004 [P] [US1] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfSignatureNoteReaderTests.cs` (xUnit + FluentAssertions) with the tests for the core cases, using `SignedPdfFixture`: one signature with `D:20260314103200+01'00'` gives `file firmato con firma digitale — 2026-03-14 10:32:00 +01:00`; `D:20260314103200Z` gives `... 10:32:00 +00:00`; `D:20260314103200` (no zone) gives `... 10:32:00` with no offset (research R3); a PDF with no form and a PDF with a form but no signature give no notes
- [X] T005 [P] [US1] Extend `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfDocumentParserTests.cs`: change the field to `new PdfDocumentParser(new PdfSignatureNoteReader())` (research R7); add an end-to-end test that a signed PDF's text is the note line, a blank line, then the page text; add a test that an unsigned PDF built with `BuildPdf` returns exactly the page text with no note and no leading blank line (FR-006); add a Moq test with a `Mock<IPdfSignatureNoteReader>` that returns two notes and checks the parser puts both before the page text, and one that returns an empty list and checks the page text is unchanged

### Implementation for User Story 1

- [X] T006 [US1] Create `PdfSignatureNoteReader` in `src/Scriptorium.Infrastructure/Parsing/PdfSignatureNoteReader.cs`: a `public sealed class` implementing `IPdfSignatureNoteReader` (T003), with `IReadOnlyList<string> Read(PdfDocument pdf)` that gets the form with `TryGetForm`, takes the `AcroSignatureField`s, resolves each field's `/V` through `pdf.Structure.GetObject`, reads `/M` and builds one line per signature; the date is formatted `yyyy-MM-dd HH:mm:ss` through `DateFormatHelper.TryParseDateTimeOffset`, with the offset appended only when the raw string has a zone designator (`Z`, `+hh'mm'`, `-hh'mm'`). Keep every method under 30 lines (private methods for finding the signature dictionaries, reading the date, formatting the line). No new package, no `!` without a comment
- [X] T007 [US1] Change `PdfDocumentParser` in `src/Scriptorium.Infrastructure/Parsing/PdfDocumentParser.cs`: receive `IPdfSignatureNoteReader` by constructor and keep it in a `_camelCase` field; decide "No extractable text" on the page text alone, then return the notes (one per line), a blank line and the page text when there is at least one note, and the page text unchanged when there is none (research R4); leave the encrypted and corrupted `catch` blocks as they are
- [X] T008 [US1] Register `IPdfSignatureNoteReader` with `PdfSignatureNoteReader` as a singleton in `src/Scriptorium.API/Program.cs`, before the existing `AddSingleton<IDocumentParser, PdfDocumentParser>()` line (research R7)
- [X] T009 [US1] Run `dotnet build` and `dotnet test`: T004 and T005 now pass, every pre-existing test is still green, and there are no new build warnings compared with T001

### Manual check for User Story 1

- [X] T010 [US1] Write a throwaway generator outside the repository (scratchpad, same technique as the research spike, not committed) and run it to put in `~/Desktop/prove-scriptorium` two fake PDFs with different content: `signed-one.pdf` (one signature, date with zone) and an unsigned PDF whose content differs from the other fake files already there. No real document is used
- [ ] T011 [US1] With the user: run quickstart scenarios 1, 2, 5, 6 and 7 against the API (throwaway database) with Ollama and `qwen3.5:4b` (each question asked 3 times in a row, as SC-003 and the quickstart require), and wait for the user's OK before anything is committed; record the outcome in `specs/003-pdf-digital-signature/validation-results.md`

**Checkpoint**: A signed PDF shows the note and answers the two questions; an unsigned one is unchanged. This is the MVP.

---

## Phase 4: User Story 3 - See who signed (Priority: P2)

**Goal**: The note says who signed: the signature's `/Name` when it is not blank, otherwise the name of the signer's certificate read from `/Contents`, never guessed, cleaned to one line of at most 100 characters (FR-011 to FR-015). Added on 2026-11-09 at the user's request; research R8-R13.

**Independent Test**: Upload a fake PDF whose signature has a name field and one that has only a certificate: both notes read `... da <name> — <date>`. Ask "who signed this document?" 3 times and get the name each time. A signature with neither shows `(firmatario sconosciuto)`.

### Interfaces for User Story 3 (before the tests and the code)

- [ ] T012 [US3] Create the two interfaces in `src/Scriptorium.Infrastructure/Parsing/`: `ISignerNameReader.cs` with `string? Read(DictionaryToken signature)` (PdfPig `UglyToad.PdfPig.Tokens.DictionaryToken` of the signature dictionary; returns the cleaned name or null; never throws) and `ICmsSignerCertificateReader.cs` with `string? ReadSignerName(ReadOnlyMemory<byte> contents)` (the bytes of `/Contents`; returns the signer certificate's common name or null when it cannot be read or the signer cannot be identified with certainty; never throws). Both documented, both in Infrastructure, not Core (research R12)

### Tests for User Story 3 (write first, they must fail) ⚠️

- [ ] T013 [US3] Extend `tests/Scriptorium.Infrastructure.Tests/Parsing/SignedPdfFixture.cs` and `FakeSignature`: `SignerName` (written as the literal `/Name`), an option to write `/Name` as a UTF-16 hexadecimal string, and a certificate option for `/Contents` (signer common name, an optional authority certificate placed before the signer's, whether the signer-info points to the signer by issuer and serial number or by subject key identifier or to nothing). Build the fake certificates with the runtime's `CertificateRequest` and the signed-data structure with `AsnWriter`, as in the research spike; pad `/Contents` with zero bytes. No new package
- [ ] T014 [P] [US3] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/CmsSignerCertificateReaderTests.cs`: signer after the authority returns the signer's name (not the authority's); signer first also works; matched by subject key identifier; signer-info pointing to no certificate returns null (no guess); bytes that are not a signed-data structure return null and throw nothing; trailing zero padding is accepted
- [ ] T015 [P] [US3] Create `tests/Scriptorium.Infrastructure.Tests/Parsing/SignerNameReaderTests.cs` with a `Mock<ICmsSignerCertificateReader>` and `DictionaryToken`s built in the test: a literal `/Name` wins over the certificate (certificate reader not called); a hexadecimal `/Name` starting with `FE FF` is decoded as UTF-16 with its accents, other hexadecimal bytes are read as Latin-1; a blank or missing `/Name` falls through to the certificate reader; neither gives null; control characters and line breaks become spaces, repeated spaces collapse, the result is trimmed and cut at 100 characters; a name that is empty after cleaning gives null; a certificate reader that throws gives null (FR-015)
- [ ] T016 [US3] Update `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfSignatureNoteReaderTests.cs` and `PdfDocumentParserTests.cs`: every `new PdfSignatureNoteReader()` becomes `new PdfSignatureNoteReader(new SignerNameReader(new CmsSignerCertificateReader()))`; add Moq tests with `Mock<ISignerNameReader>` for the line (name and date with zone, name and no date, unknown signer and a date, unknown signer and no date); update the existing expectations of the earlier tests, because a note with no name now reads `... (firmatario sconosciuto)`; add end-to-end tests with the fixture for a `/Name`, for a certificate only, for both (name field wins) and for an injection attempt (a name with a line break stays on one line)

### Implementation for User Story 3

- [ ] T017 [US3] Create `CmsSignerCertificateReader` in `src/Scriptorium.Infrastructure/Parsing/CmsSignerCertificateReader.cs`, a `public sealed class` implementing `ICmsSignerCertificateReader`: read the signed-data structure with `System.Formats.Asn1` (BER rules), load each certificate with `X509CertificateLoader.LoadCertificate` in its own try/catch, read the first signer-info and pick the certificate it points to by issuer and serial number (serials compared without leading zeros) or by subject key identifier, return `GetNameInfo(X509NameType.SimpleName, false)`; return null instead of guessing, catch every exception except `OperationCanceledException`. Methods under 30 lines (research R10)
- [ ] T018 [US3] Create `SignerNameReader` in `src/Scriptorium.Infrastructure/Parsing/SignerNameReader.cs`, a `public sealed class` implementing `ISignerNameReader` and receiving `ICmsSignerCertificateReader` by constructor: read `/Name` (a `StringToken`'s `Data`, or a `HexToken`'s bytes decoded per R9); if blank, resolve `/Contents` (a `HexToken`) and ask the certificate reader; clean the result (control characters and line breaks to spaces, collapse spaces, trim, cut at 100); empty gives null; catch every exception except `OperationCanceledException` and return null (research R8, R9, R11)
- [ ] T019 [US3] Change `PdfSignatureNoteReader` in `src/Scriptorium.Infrastructure/Parsing/PdfSignatureNoteReader.cs`: receive `ISignerNameReader` by constructor and build the line as `file firmato con firma digitale` + ` da <name>`, or ` (firmatario sconosciuto)` when the name reader gives null (FR-016), + ` — <date>` when a date is known (data-model.md, contracts/extracted-text.md)
- [ ] T020 [US3] Register `ICmsSignerCertificateReader` with `CmsSignerCertificateReader` and `ISignerNameReader` with `SignerNameReader` as singletons in `src/Scriptorium.API/Program.cs`, before the `IPdfSignatureNoteReader` line
- [ ] T021 [US3] Run `dotnet build` and `dotnet test`: T014, T015 and T016 now pass, every earlier test is still green, no new build warnings compared with T001, and `git diff` shows no change to any `.csproj` (FR-009)

### Manual check for User Story 3

- [ ] T022 [US3] Extend the throwaway generator outside the repository (a small .NET program using only the runtime, because certificates are needed; not committed) and run it to add to `~/Desktop/prove-scriptorium`, each with different content: `signed-with-name.pdf`, `signed-with-certificate.pdf`, `signed-name-and-certificate.pdf`, `signed-certificate-unmatched.pdf`, `signed-name-blank.pdf`, `signed-name-injection.pdf` and `signed-no-signer-info.pdf`. Fake names only (for example "Mario Rossi" / "ROSSI MARIO"); no real person or document
- [ ] T023 [US3] With the user: run quickstart scenarios 9, 10, 11 and 12 against the API (each question 3 times in a row) and wait for the user's OK before anything is committed; record the outcome in `specs/003-pdf-digital-signature/validation-results.md`

**Checkpoint**: The note says who signed whenever the file gives a name, says "firmatario sconosciuto" otherwise, and never invents one.

---

## Phase 5: User Story 2 - Handle several or incomplete signatures (Priority: P2)

**Goal**: Several signatures give one note each in the form's field order; a signature without a readable date gives the note without a date; a placeholder gives nothing; a malformed form never fails the upload (FR-003, FR-004, FR-007, FR-010).

**Independent Test**: Upload a PDF with two signatures (two notes), one with no date and one with an unreadable date (note without date), one with an empty signature field (no note), and a signed scan (still rejected).

### Tests for User Story 2 (write first, they must fail) ⚠️

- [ ] T024 [P] [US2] Extend `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfSignatureNoteReaderTests.cs` with: two signatures give two notes in the order of `/Fields`, including a fixture whose `/Fields` lists the higher object number first (so the order is the form's, not the file's); no `/M` gives the bare wording; `not a date`, `D:2026` and `D:20260314103200+0100` each give the bare wording (research R2-R3); a field without `/V` gives no note; a signature inside a nested field (`/Kids`) is found; a malformed form returns no notes and throws nothing (FR-010)
- [ ] T025 [P] [US2] Extend `tests/Scriptorium.Infrastructure.Tests/Parsing/PdfDocumentParserTests.cs` with: a PDF with two signatures starts with two note lines, a blank line and the page text; a PDF with only an empty signature field returns the page text unchanged; a signed PDF with no page text still fails with `No extractable text` (the note alone does not count); a PDF with a malformed form still returns its page text with no note

### Implementation for User Story 2

- [ ] T026 [US2] Harden `PdfSignatureNoteReader` in `src/Scriptorium.Infrastructure/Parsing/PdfSignatureNoteReader.cs`: flatten nested fields with `AcroFormExtensions.GetFields`; skip any signature field whose `/V` is missing or does not resolve to a dictionary; treat a `/M` with fewer than date, hour and minute (12 digits after `D:`) or that `DateFormatHelper` cannot parse as "no date"; catch any exception except `OperationCanceledException` inside `Read` and return an empty list (research R5-R6). Keep each method under 30 lines
- [ ] T027 [US2] Run `dotnet build` and `dotnet test`: T024 and T025 now pass, all earlier (US1, US3 and pre-existing) tests still green, no new build warnings compared with T001
- [ ] T028 [US2] Extend the throwaway generator (outside the repository) and run it to add to `~/Desktop/prove-scriptorium`, each with different content: `signed-two.pdf`, `signed-no-date.pdf`, `signed-bad-date.pdf`, `signature-placeholder.pdf` and `signed-scan.pdf` (signed, no page text)
- [ ] T029 [US2] With the user: run quickstart scenarios 3 and 4 against the API and wait for the user's OK before anything is committed; record the outcome in `specs/003-pdf-digital-signature/validation-results.md`

**Checkpoint**: Both stories work; every edge case in the spec behaves as written.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, review against the constitution and the final regression run

- [ ] T030 [P] Add one line to the "What it does today" list in `README.md` saying that, for a digitally signed PDF, the extracted text starts with the note "file firmato con firma digitale", followed by the signer's name when the file gives one (from the signature's name field or its certificate) and by the signing date and time; the name is reported as declared, with no validity check
- [ ] T031 Review the new and changed code against the constitution: `PdfSignatureNoteReader`, `SignerNameReader` and `CmsSignerCertificateReader` each have one responsibility, every method is under 30 lines, no `!` without a comment, no `.Result`/`.Wait()`, no reflection, the parser and the readers depend on each other only through their interfaces and constructors; confirm with `git diff` that no `.csproj` file and no package reference changed (FR-009); list any deviation and ask the user before fixing anything beyond the feature
- [ ] T032 Run `dotnet build` and the full `dotnet test`, compare with the T001 baseline (SC-004), run quickstart scenario 8, and complete `specs/003-pdf-digital-signature/validation-results.md` with the results of scenarios 1 to 12

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **Foundational (Phase 2)**: after Setup; blocks all story tests
- **User Story 1 (Phase 3)**: after Foundational; no dependency on the other stories
- **User Story 3, who signed (Phase 4)**: after User Story 1, because it changes the same class (`PdfSignatureNoteReader`) and the same test files and fixture; the interfaces (T012) come before its tests and code. Done before User Story 2 because the user ranked the signer's name as important (2026-11-09)
- **User Story 2, several or incomplete signatures (Phase 5)**: after User Story 3, because it extends the same reader and tests again; it is independent in behavior but not in files
- **Polish (Phase 6)**: after all three stories

### Within Each Story

- Tests first (T004/T005, T014-T016, T024/T025), then confirm they fail, then the code
- T003 (interface) before the tests and the code; T006 (reader) before T007 (parser uses the interface) before T008 (DI registration)
- Story 3: T012 (interfaces) and T013 (fixture) before T014-T016; T017 and T018 before T019 (the note reader uses the name reader) before T020 (DI registration)
- Build and test (T009, T021, T027) before the manual check
- The manual check (T011, T023, T029) before any commit

### Parallel Opportunities

- T004 and T005 are in different files and can be written together
- T014 and T015 are in different files and can be written together (after T012 and T013)
- T024 and T025 are in different files and can be written together
- T030 (README) can be done at any time after T007
- T006, T019, T026 and T007 touch the same or already-ordered files and are **not** parallel

### Parallel Example: User Story 1

```text
# After T002 and T003, write both test files together:
Task: "T004 Reader tests in tests/Scriptorium.Infrastructure.Tests/Parsing/PdfSignatureNoteReaderTests.cs"
Task: "T005 Parser tests in tests/Scriptorium.Infrastructure.Tests/Parsing/PdfDocumentParserTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. T001 baseline, T002 fixture helper, T003 interface
2. T004-T009 tests then code for a single dated signature
3. T010-T011: try it in the browser, ask the two questions
4. **Stop and validate**: the user tries it and gives the OK; only then commit (on `004-pdf-digital-signature`, via PR)

### Incremental Delivery

1. Foundation + User Story 1 → tried by the user → commit
2. User Story 3 (who signed) → tried by the user → commit
3. User Story 2 (several or incomplete signatures) → tried by the user → commit
4. Polish: README, review, final regression run → commit → PR

---

## Notes

- [P] tasks = different files, no dependencies
- The reader must never read the signature bytes (`/Contents`); only the dictionary entries `/V` and `/M` (plan, Performance Goals)
- Fake PDFs only: no document uploaded by the user is read or used for tests or diagnosis
- If a task shows PdfPig behaves differently from research.md (R1-R5), stop and update research.md before going on, and tell the user
