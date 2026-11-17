# Research: Signature Author and Signature Date Buttons

Decisions taken before writing code. Everything here was checked against the repository or with a small throwaway program
(a spike) that is not part of the project.

## R1. How are digital signatures stored in a Word or Excel file?

**Decision**: Read them from the package's signature part with the library already used for the text (`DocumentFormat.OpenXml`,
3.5.1): `DigitalSignatureOriginPart.XmlSignatureParts`, one `XmlSignaturePart` per signature, in the order the library returns
them. Each part is an XML-DSig document read with `System.Xml.Linq` (part of the runtime).

**Rationale**: The spike built a Word file with a signature origin part and two signature parts, closed it, opened it again and
read both signatures back, in the order they were written. The same property is on the base package class, so Excel
(`SpreadsheetDocument`) works the same way (a test will prove it). No new package.

**Alternatives considered**:
- *`System.IO.Packaging`*: another package model next to the one already in use. Rejected.
- *Unzipping the file by hand and reading `_xmlsignatures/*.xml`*: reimplements what the library does. Rejected.

## R2. Where do the signer and the date come from?

**Decision**:
- **Signer**: the common name (simple name) of the **signer's own certificate** among the `X509Certificate` entries of
  `KeyInfo/X509Data`, read with the runtime's `X509CertificateLoader` (the same call `CmsSignerCertificateReader` uses). A
  signature made by a certification provider often carries the whole chain (the provider's certificate authority and root as well),
  in no fixed order, so the first one is not always the signer. The signer's certificate is the first, in file order, that is **not a
  certificate authority** (no `BasicConstraints` with `CA` true) and **did not issue another certificate of the same signature**;
  if no certificate passes both tests, the first one is used. Cleaned as for PDFs: one line, at most 100 characters. No
  certificate, or none that can be read: unknown signer. Nothing else is guessed.
- **Date**: the time the signature declares about itself, written as an ISO 8601 date and time with an optional zone
  (`2026-03-14T10:32:00+01:00`, `...Z`). It is read first from the Office `SignatureTime/Value` and, when that is missing, from
  the XAdES `SigningTime` (a signature made by another program, such as a certification provider's, may carry only that one;
  matched by its local name, whatever the namespace version). It needs at least date, hour and minute, as for PDFs. Anything
  else is "no date".

**Rationale**: A Word or Excel signature has no separate "name" field like a PDF's `/Name`; the certificate is where the signer is
declared. The spike read `CN=Mario Rossi Prova` and both dates exactly as written. Showing the date as declared (no conversion)
follows R3 of feature 003.

**Alternatives considered**: *The first certificate*: shows the name of the authority when a provider's chain lists it first.
Rejected. *Matching the XAdES `SigningCertificate` digest*: exact, but only some programs write it; may be added later if a real
file needs it. *Only `SignatureTime`*: loses the date of signatures made by programs that write only the XAdES time. Rejected. *The visible signature line text (`SuggestedSigner`)*: it names who was asked to sign, not who
signed. Rejected. *The file's modification time*: not a signing time. Rejected.

## R3. What is a "signature" for the buttons: the line format

**Decision**: Reuse the line that feature 003 writes, character for character:
`file firmato con firma digitale da <name> — <yyyy-MM-dd HH:mm:ss[ ±hh:mm]>`, with `(firmatario sconosciuto)` for an unknown
signer and no ` — date` when there is no date. The wording and the layout move into one small class in Core (`SignatureNote`)
that both **writes** a line (used by the PDF reader and the new Word/Excel reader) and **reads** it (used by the buttons).

**Rationale**: One definition of the format cannot drift between the writer and the reader; a round-trip test (write then read)
proves they agree. Core may know the wording because it is the product's own text, not infrastructure.

**Alternatives considered**:
- *A second copy of the wording in Core*: two places to keep equal. Rejected.
- *Storing the signatures as data (a table or a column)*: a migration and a rewrite of all documents already stored. Rejected
  (spec FR-013: nothing is stored).

## R4. Which lines count as signatures when the text is read

**Decision**: The **leading block** of the stored text: consecutive lines, from the first line, that start with the wording and
follow the layout, ended by a blank line or by the end of the text. Lines further down never count. Only documents of type
`pdf`, `docx` or `xlsx` are looked at.

**Rationale**: It is exactly how the writers put them (notes, then a blank line, then the text). A text file, or a quotation of the
wording inside a document, is not at the start followed by a blank line, or is not of a signable type.

**Known limit (accepted)**: nothing but the text is stored, so a PDF, Word or Excel file that is *not* signed but whose very first
lines are exactly in the note format followed by a blank line would be read as signed. A real fix needs stored metadata (a
migration); the user decided against storing anything. Said in the README.

**Alternatives considered**: *Look at every line of the text*: a quoted line anywhere would count. Rejected (FR-006).

## R5. How the Word and Excel parsers get the notes without being edited

**Decision**: A decorator, `SignedOfficeDocumentParser`, wraps the existing `WordDocumentParser` and `ExcelDocumentParser`. It
asks the inner parser for the text, then reads the signatures of the same stream and puts the lines in front. The two parsers and
their tests are not touched.

**Rationale**: The parser tests build the parsers with no constructor argument (`new WordDocumentParser()`); giving them a reader
would force edits to existing tests (spec FR-015). A decorator keeps each class to one responsibility, adds the behaviour without
changing the old classes and lets one class serve both types.

**Alternatives considered**: *Add a constructor parameter to both parsers*: edits existing tests. *A second parameterless
constructor*: two ways to build the class and a hidden default, against constructor injection. Both rejected.

## R6. The "no text" rule for Word and Excel

**Decision**: Word and Excel never rejected an empty text and still do not. With signatures the text is the lines, then a blank
line, then the text; when the text is empty it is just the lines. An unsigned empty document stays empty, as today.

**Rationale**: Feature 003 (R4) kept a signed *scanned PDF* rejected because the rule there is "no extractable text". Word and
Excel have no such rule, so a signed document with no body is still accepted, and its signature is still recorded and shown.

## R7. Which signatures are skipped or fail

**Decision**: The signature XML comes from an upload that is not trusted, so it is read with DTD processing **prohibited** and no
external resolver; a part with a `DOCTYPE` or entities is treated as unreadable. A signature part whose XML cannot be read gives
no line. A readable one whose certificate is missing or cannot be
read gives a line with an unknown signer. Any other exception while reading signatures is caught and gives no lines at all, so
the upload goes on and the text is exactly what it was before (spec FR-014). Cancellation is not swallowed.

**Rationale**: Same policy as R6 of feature 003.

## R8. How the buttons get their data

**Decision**: One read-only endpoint, `GET /api/documents/{id}/signatures`, returns the list of signatures (signer and date as
text, or null). **Signature author** shows the signers and **Signature date** the dates from the same answer; each button makes the
call itself. No model, no preference, no body.

**Rationale**: Both buttons need the same facts; two endpoints would duplicate everything. A read that changes nothing is a `GET`,
unlike the model endpoints, which are `POST`. The failures are the document ones already mapped (`404`, `409`, `500`).

**Alternatives considered**: *A client that parses the text itself*: the page would need the full text and the parsing rules
(business logic in a view). Rejected. *Two endpoints*: duplication. Rejected.

## R9. The text read, and the model

**Decision**: The signatures are read from the whole stored text (not the 8,000-character cut) through `ReadyDocumentText`, which
gains the document's `FileType` so the type can be checked. The router, the scorer and the providers are not involved at all.

**Rationale**: FR-008 and FR-009. Adding a property to `ReadyDocumentText` changes no existing behaviour.

## R10. Tests for Word and Excel signatures without real people

**Decision**: The tests build the signed files themselves: a Word or Excel file made with the library, a signature origin part,
and signature parts holding a hand-written XML-DSig body, with a self-signed certificate created in the test
(`CertificateRequest`, in the runtime) whose subject is an invented name. The signature value is not a real signature and is
never checked.

**Rationale**: The spike did exactly this and it read back. No real certificate, no real document, no secret, no package.
