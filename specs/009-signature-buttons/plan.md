# Implementation Plan: Signature Author and Signature Date Buttons

**Branch**: `013-signature-buttons` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-signature-buttons/spec.md`

## Summary

Two buttons, **Signature author** and **Signature date**, give the signers and the signing dates of a ready PDF, Word or Excel
file with one click and no model. The work has two halves:

1. **Word and Excel signatures are written at upload** (new). A decorator, `SignedOfficeDocumentParser`, wraps the two existing
   parsers, reads the signature parts of the package and puts one line per signature in front of the text, in the format that
   feature 003 already uses for PDFs. The two old parsers and their tests are not edited.
2. **The buttons read those lines** (new). One `GET /api/documents/{id}/signatures` endpoint reads the leading lines of the
   stored text through a Core service and returns the entries; the page shows them in two cards. The router, the scorer and the
   providers are not involved.

The format of the line is defined once, in a new Core class `SignatureNote`, which both writes it (PDF and Office readers) and
reads it (the service). The signer's name cleaning is moved out of `SignerNameReader` so the Office reader can share it. No
migration, no new package.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (backend); TypeScript on Angular 19 (page)

**Primary Dependencies**: no new package. `DocumentFormat.OpenXml` 3.5.1 (already used by the parsers) for the signature parts;
`System.Xml.Linq` and `X509CertificateLoader` from the runtime for the signature XML and the certificate

**Storage**: none new. The lines live in the existing `ExtractedText`; nothing is stored by the buttons

**Testing**: xUnit + FluentAssertions + Moq; tests written first and seen failing; fake signed Word and Excel files built in the
tests with a self-signed certificate of an invented name; in-process API tests (`ApiTestHost`); Karma/Jasmine for the page

**Target Platform**: macOS and Linux, local web use

**Project Type**: web application (API + Angular page)

**Performance Goals**: a click answers in under 1 second (a read of the stored text, no model)

**Constraints**: no model, no router, no cloud; whole stored text read; methods under 30 lines; existing tests unedited; no new
package; fake files only

**Scale/Scope**: one new endpoint, one decorator, one reader, one Core service and parser class, two buttons and two cards

## Constitution Check

*GATE: passes before Phase 0 and after Phase 1.*

| Principle | Check |
|---|---|
| I. Clean Architecture | `SignatureNote`, the service and the result are in Core and use only `IDocumentRepository`. The OpenXml and certificate reading is in Infrastructure. The endpoint only calls the service and maps the result. **Pass** |
| II. Async-First | the service is `async`; the readers are synchronous CPU work on a stream already in memory, as the parsers' own `Extract` is. No `.Result`, no `.Wait()`. **Pass** |
| III. Nullable safety | no `!`; `signer` and `signedAt` are nullable by design. **Pass** |
| IV. Result pattern | `SignatureResult` carries success or a failure kind; no exception crosses a layer. **Pass** |
| V. Constructor injection | the decorator and the service take their collaborators in the constructor; the two inner parsers are created in the registration, not looked up. **Pass** |
| VI. Single responsibility, methods under 30 lines | one job per class (write the line, read the line, read Office signatures, decorate a parser, serve the signatures); methods are small. **Pass** |
| VII. Provider isolation | no model is called, so no provider and no `ModelRouter` is touched; no `HttpClient`. **Pass** |
| VIII. Test parity | each new class gets a test class. **Pass** |
| No new package | none. **Pass** |
| No secrets, cloud never called | nothing about the cloud; the test certificate is self-made with an invented name. **Pass** |

No violation, so there is nothing in Complexity Tracking.

## Points for the user

These are my choices, not yours yet. Say if you want any changed:

1. **A decorator instead of editing the Word and Excel parsers** (R5). The two parsers and their tests stay exactly as they are; a
   small wrapper adds the signature lines. The price is one more class and a slightly different registration in `Program.cs`.
2. **The line format moves into Core** (R3). `PdfSignatureNoteReader` will build its line with the shared class instead of its own
   private text; its tests are not edited and must keep passing as proof.
3. **One endpoint for both buttons** (R8). `GET /api/documents/{id}/signatures`; each button calls it and shows its part.
4. **The leading block rule and its known limit** (R4): a file that is *not* signed but starts with lines exactly in the note
   format, then a blank line, would be read as signed. It cannot be told apart without storing something, and you decided to
   store nothing. The README will say it.
5. **Signer and date of a Word or Excel signature** (R2): the signer is the common name of the signer's own certificate (when a provider's chain is present, the one that is not a certification authority and did not issue the others) and the
   date is the signature's declared time (the Office `SignatureTime`, or the XAdES `SigningTime` when only that is there, for signatures made by other programs); no other source, nothing guessed, nothing validated.
6. **A signed Word or Excel file with no text** is accepted and shows its signatures (R6); for a scanned PDF the old rule
   (rejected) stays.
7. **Files uploaded before** have no lines and show "no digital signature". To fix one, delete it and upload it again (the same
   content is refused as a duplicate while the old one exists).

## Project Structure

### Documentation (this feature)

```text
specs/009-signature-buttons/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/signatures-api.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks, not created here
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Core/
│   ├── Services/SignatureNote.cs                 NEW: the line format, written and read in one place
│   ├── Services/DocumentSignatureService.cs      NEW: the use case "the signatures of a ready document"
│   ├── Services/ReadyDocumentText.cs             + FileType on a loaded document (nothing else changes)
│   ├── Results/SignatureEntry.cs                 NEW: signer and signed-at, both nullable
│   ├── Results/SignatureResult.cs                NEW: entries, or a failure kind and a message
│   └── Dtos/SignatureDtos.cs                     NEW: SignatureDto, SignaturesResponseDto
├── Scriptorium.Infrastructure/Parsing/
│   ├── SignerNameText.cs                         NEW: the name cleaning taken out of SignerNameReader
│   ├── SignerNameReader.cs                       uses SignerNameText (same behaviour)
│   ├── PdfSignatureNoteReader.cs                 builds its line with SignatureNote (same text)
│   ├── IOfficeSignatureNoteReader.cs             NEW
│   ├── OfficeSignatureNoteReader.cs              NEW: reads the signature parts of a docx or xlsx
│   └── SignedOfficeDocumentParser.cs             NEW: the decorator over the Word and Excel parsers
├── Scriptorium.API/
│   ├── Endpoints/SignatureEndpoints.cs           NEW: GET /api/documents/{id}/signatures
│   └── Program.cs                                registers the reader, the decorated parsers, the service; maps the endpoint
└── Scriptorium.Web/src/app/documents/
    ├── document.models.ts                        + SignatureEntry, SignaturesResponse
    ├── document-api.service.ts                   + getSignatures(id)
    └── ask/ask-panel.component.{ts,html}, styles.css   two buttons, two cards, messages
tests/
├── Scriptorium.Core.Tests/       SignatureNoteTests, DocumentSignatureServiceTests, SignatureResultTests, SignatureDtosTests,
│                                  ReadyDocumentText (+ FileType)
└── Scriptorium.Infrastructure.Tests/
    ├── Parsing/                  OfficeSignatureNoteReaderTests, SignedOfficeDocumentParserTests, SignedOfficeFixture,
    │                              SignerNameTextTests, + a round-trip test with the PDF reader
    └── Endpoints/                SignatureEndpointTests
specs docs: README.md, docs/architecture.md
```

**Structure Decision**: the existing four-project layout. The format and the use case are in Core, the file reading is in
Infrastructure, the endpoint and registration in the API, the buttons in the page.

## Phase 0 and Phase 1 outputs

- [research.md](research.md): R1 to R10, all resolved; no open question.
- [data-model.md](data-model.md): the line format, the entry, the result, which lines count.
- [contracts/signatures-api.md](contracts/signatures-api.md): the endpoint.
- [quickstart.md](quickstart.md): how to check it with the user.

## Complexity Tracking

Nothing to justify.
