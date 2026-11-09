# Implementation Plan: Digital Signature Note for Signed PDFs

**Branch**: `004-pdf-digital-signature` | **Date**: 2026-11-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-pdf-digital-signature/spec.md`

## Summary

A digitally signed PDF carries its signature as form data, not as text on the page, so the text Scriptorium
extracts never says the file is signed. The PDF parser will look for signatures and, for each one, put a short
line at the start of the extracted text: `file firmato con firma digitale`, followed by the signing date and time
when the signature declares a readable one. Unsigned PDFs are left exactly as they are. The signature lookup is a
new small class next to the parser, built only on PdfPig 0.1.16 (already referenced), so no new package is needed:
a spike confirmed that PdfPig exposes the signature fields and the signing date (see [research.md](./research.md)).
Nothing outside the PDF parser changes: the note is part of the extracted text, so storage, the text endpoint and
the question flow of feature 002 pick it up with no further work.

**Added 2026-11-09 (User Story 3, who signed):** the line also says who signed: `file firmato con firma digitale da
Mario Rossi — 2026-03-14 10:32:00 +01:00`. The name is taken from the signature's `/Name` entry when it has one, and
otherwise from the signer's certificate inside the signature's `/Contents`, read with `System.Formats.Asn1` and
`X509CertificateLoader`, which are part of the .NET runtime (no new package; confirmed by a second spike, research R8-R13).
Two more small classes, each behind an interface, do this work: one chooses and cleans the name, one reads the certificate.
When no name can be extracted the note says `(firmatario sconosciuto)` (FR-016), so every note carries a signer.

**Language/Version**: C# 14 / .NET 10 (backend); the Angular frontend is not touched

**Primary Dependencies**: PdfPig 0.1.16 (existing, `Scriptorium.Infrastructure` only). For the signer's certificate
(User Story 3): `System.Formats.Asn1` and `System.Security.Cryptography.X509Certificates`, both already in the .NET 10
runtime. No new package.

**Storage**: None added. The note becomes part of the existing `ExtractedText`; no schema change, no migration.
Documents uploaded before this feature keep their old text until re-uploaded.

**Testing**: xUnit + FluentAssertions + Moq. Signed PDFs are fake files written by the tests as raw PDF text (the
same technique as the encrypted-PDF fixture already in `PdfDocumentParserTests`); `PdfDocumentBuilder` cannot create
form fields. The behavior with the real model is checked manually through [quickstart.md](./quickstart.md).

**Target Platform**: Local macOS web app (unchanged)

**Project Type**: Web application; this feature touches only the backend parsing layer

**Performance Goals**: No measurable slowdown of upload processing: the lookup reads the form dictionary and, only when
the signature has no `/Name`, parses the signature's `/Contents` (a few KB) to read one certificate; no cryptographic
operation is run (SC-004 is "no upload fails because of it", checked on the existing PDF tests)

**Constraints**: No new NuGet package (FR-009); no validity check and no verification of the signer's identity (FR-008);
a failure while looking for signatures or names must never fail the upload (FR-010, FR-015); the signer's name is shown
as one clean line of at most 100 characters (FR-014); the existing rejections for encrypted, corrupted and image-only
PDFs stay as they are

**Scale/Scope**: Three new interfaces and three new classes, one modified class, a few DI lines; about 200 lines of
production code in total (about 70 for stories 1-2, about 130 for story 3)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Clean Architecture | Signature reading uses PdfPig, so it lives in `Scriptorium.Infrastructure/Parsing`; `Scriptorium.Core` and `IDocumentParser` are unchanged | PASS |
| II. Async-First | `ExtractTextAsync` keeps its shape; the lookup is pure CPU on an already-open document, with no I/O of its own | PASS |
| III. Nullable Reference Safety | Missing form, missing `/V`, missing `/M` are all modelled as null/empty; no `!` | PASS |
| IV. Result<T> | The parser still returns `Result<string>`; failures inside the signature lookup are absorbed there (FR-010) instead of escaping as exceptions | PASS |
| V. Constructor Injection | `PdfDocumentParser` receives `IPdfSignatureNoteReader` by constructor; registered in `Program.cs` | PASS |
| VI. Single Responsibility & Simplicity | Building the note, choosing and cleaning the signer name, and parsing the certificate are three separate classes; the parser only joins the notes and the page text; methods stay under 30 lines | PASS |
| VII. Provider Isolation | No AI code involved | PASS |
| VIII. Test Parity | New `PdfSignatureNoteReaderTests`, `SignerNameReaderTests`, `CmsSignerCertificateReaderTests`; `PdfDocumentParserTests` extended | PASS |
| Workflow: interface before implementation | `IPdfSignatureNoteReader`, `ISignerNameReader` and `ICmsSignerCertificateReader` are written before their classes (T003, T018) | PASS |
| Non-negotiable: no new NuGet | Confirmed by the spikes: only PdfPig and runtime types are used (no `System.Security.Cryptography.Pkcs`, which is a package) | PASS |

**Post-design re-check (after Phase 1)**: unchanged, all PASS. No entry in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/003-pdf-digital-signature/
├── plan.md                      # This file
├── research.md                  # Phase 0: what PdfPig exposes, date handling, decisions
├── data-model.md                # Phase 1: the signature note
├── quickstart.md                # Phase 1: manual validation scenarios
├── contracts/
│   └── extracted-text.md        # Phase 1: how the note appears in the extracted text
├── checklists/
│   └── requirements.md          # From /speckit-specify
├── tasks.md                     # Phase 2 (/speckit-tasks, not created here)
└── validation-results.md        # Created at the first manual check, completed at the end
```

### Source Code (repository root)

```text
src/
├── Scriptorium.Infrastructure/
│   └── Parsing/
│       ├── IPdfSignatureNoteReader.cs  # NEW: contract of the reader (Infrastructure, not Core)
│       ├── PdfSignatureNoteReader.cs   # NEW: finds the signatures, builds the notes
│       ├── ISignerNameReader.cs        # NEW (story 3): contract: name of the signer of one signature
│       ├── SignerNameReader.cs         # NEW (story 3): /Name first, then the certificate; cleans the name
│       ├── ICmsSignerCertificateReader.cs  # NEW (story 3): contract: signer's name from the /Contents bytes
│       ├── CmsSignerCertificateReader.cs   # NEW (story 3): reads the signed-data structure and the certificate
│       └── PdfDocumentParser.cs        # CHANGED: prepends the notes to the page text
└── Scriptorium.API/
    └── Program.cs                      # CHANGED: registers the reader interfaces

tests/
└── Scriptorium.Infrastructure.Tests/
    └── Parsing/
        ├── PdfSignatureNoteReaderTests.cs  # NEW: one test per case of the spec
        ├── SignerNameReaderTests.cs        # NEW (story 3): which source wins, blank, cleaning, cut at 100
        ├── CmsSignerCertificateReaderTests.cs  # NEW (story 3): fake certificates built with the runtime
        ├── PdfDocumentParserTests.cs       # CHANGED: signed PDF end to end, unsigned unchanged, mocked reader
        └── SignedPdfFixture.cs             # NEW: test helper that writes fake signed PDFs (and, story 3, fake certificates)

README.md                                   # CHANGED: one line about the signature note
```

**Structure Decision**: Keep the change inside the existing parsing folder and its test folder. The reader is a
class of its own, injected into the parser, rather than private methods of the parser, so that parsing text and
reading signatures remain separate responsibilities (Principle VI) and each has its own test class (Principle VIII).
The reader has an interface, `IPdfSignatureNoteReader`, written before the class as the constitution's workflow asks.
It sits in Infrastructure, not Core: it uses the PdfPig `PdfDocument` type and nothing outside Infrastructure needs it.
The same rule holds for the two interfaces of story 3 (`ISignerNameReader`, `ICmsSignerCertificateReader`): they live in
Infrastructure, and `PdfSignatureNoteReader` receives `ISignerNameReader` by constructor (research R12).

## Complexity Tracking

No constitution violations to justify.
