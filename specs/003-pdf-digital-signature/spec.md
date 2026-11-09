# Feature Specification: Digital Signature Note for Signed PDFs

**Feature Branch**: `004-pdf-digital-signature`

**Created**: 2026-11-09

**Status**: Draft

**Input**: User description: "PDF digital signature detection: when a PDF contains a digital signature, the text extracted by PdfDocumentParser must include the note 'file firmato con firma digitale' plus the date and time of the signature. No new NuGet packages." Added later by the user (2026-11-09): the note must also say who signed the document, taking the name from the signature's own name field when it has one, and otherwise from the signer's certificate.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See that a PDF is digitally signed (Priority: P1)

The user uploads a PDF that carries a digital signature (for example a signed contract or an official certificate). A digital signature is not printed as text on the page, so today nothing in the document's text tells the user, or the model answering their questions, that the file is signed. After this feature, the text of a signed PDF starts with a short note saying the file is digitally signed, together with the date and time of the signature.

**Why this priority**: This is the whole feature. Without the note, a question like "is this document signed?" or "when was it signed?" cannot be answered, because the information is simply absent from the text.

**Independent Test**: Upload a signed PDF and read its extracted text: the note and the signature date and time are present. Upload an unsigned PDF: the text is exactly what it was before.

**Acceptance Scenarios**:

1. **Given** a PDF with one digital signature that declares a signing date and time, **When** the user uploads it, **Then** the extracted text contains the note "file firmato con firma digitale" followed by that date and time.
2. **Given** a PDF without any digital signature, **When** the user uploads it, **Then** the extracted text contains no signature note and is identical to the text produced before this feature.
3. **Given** a signed PDF that has been uploaded, **When** the user asks "is this document digitally signed?" or "when was it signed?", **Then** the answer reflects the note and the date and time it contains.

---

### User Story 2 - Handle several or incomplete signatures (Priority: P2)

Some PDFs are signed more than once (for example by two parties), and some signatures do not declare a readable signing date. The user still wants an accurate, non-misleading note in these cases.

**Why this priority**: Less common than a single, well-formed signature, but a wrong or missing note here would mislead the user about the document.

**Independent Test**: Upload a PDF with two signatures and check the text has two notes; upload a PDF whose signature has no signing date and check the note appears without a date.

**Acceptance Scenarios**:

1. **Given** a PDF with two digital signatures, **When** the user uploads it, **Then** the extracted text contains one note per signature, each with its own date and time.
2. **Given** a PDF with a digital signature that declares no signing date, **When** the user uploads it, **Then** the note "file firmato con firma digitale" appears without a date and the upload still succeeds.

---

### User Story 3 - See who signed (Priority: P2)

The user wants to know not only that a PDF is digitally signed, but by whom. When the signature names its signer, the note says so: "file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00". The name comes from the signature's own name field when it has one; otherwise from the certificate the signer attached to the signature.

**Why this priority**: The user stated that writing who signed the document is important: a note that only says "signed" leaves the most useful question, "signed by whom?", unanswered.

**Independent Test**: Upload a PDF whose signature has a name field, and one whose signature has only a certificate: both notes show the signer's name. Ask the model "who signed it?" and get the name.

**Acceptance Scenarios**:

1. **Given** a PDF whose signature has the name field "Mario Rossi", **When** the user uploads it, **Then** the note contains "da Mario Rossi".
2. **Given** a PDF whose signature has no name field but contains a certificate issued to "ROSSI MARIO", **When** the user uploads it, **Then** the note contains "da ROSSI MARIO".
3. **Given** a signature that has both a name field and a certificate with a different name, **When** the user uploads it, **Then** the note shows the name field.
4. **Given** a signature with no name field and no certificate (or one whose signer cannot be identified with certainty), **When** the user uploads it, **Then** the note has no name, exactly as before.
5. **Given** a signed PDF with a signer name, **When** the user asks "who signed this document?", **Then** the answer gives that name.

---

### Edge Cases

- A signed PDF that contains no extractable page text (image-only scan): it keeps being rejected as it is today; the signature note alone does not count as extractable text.
- A signature whose declared date cannot be read or is malformed: the note is shown without a date; the upload does not fail.
- A signature field that exists but is empty (an unsigned placeholder for a future signature): not treated as a signature, no note is added.
- An encrypted or corrupted PDF: handled as today (rejected with the existing reason); signature detection never turns a working upload into a failure.
- A PDF whose signature is no longer valid because the content was modified after signing: the note is still shown, because the feature reports the presence of a signature, not its validity.
- A signature whose name field is empty or blank: treated as having no name field, so the certificate is tried.
- A certificate that is damaged or cannot be read: the note is shown without a name; the upload does not fail.
- A signature with several certificates (the signer's and the issuing authorities'): the name is the signer's, never an authority's; if the signer's certificate cannot be identified with certainty, no name is shown rather than a possibly wrong one.
- A name containing line breaks, control characters or a very long text (for example crafted to look like extra instructions): shown as one short line of plain text.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: For every PDF that contains a digital signature, the extracted text MUST include the note "file firmato con firma digitale".
- **FR-002**: When the signature declares a signing date and time, the note MUST include that date and time, in an unambiguous format that also shows the time zone when the signature provides it.
- **FR-003**: When a PDF has several digital signatures, the extracted text MUST include one note per signature, in the order of the document's signature fields (the order in which the PDF lists them, which is not necessarily their position in the file).
- **FR-004**: When a signature has no readable signing date, the note MUST still be added, without a date.
- **FR-005**: The note MUST appear at the beginning of the extracted text, before the page content, so it is visible even when a long document is truncated for the model.
- **FR-006**: The text of a PDF without digital signatures MUST NOT change in any way.
- **FR-007**: A signature that is only a placeholder with no signed content MUST NOT produce a note.
- **FR-008**: The feature MUST NOT verify that a signature is valid or trusted, nor check the signer's identity. The note states that a digital signature is present, when it declares to have been made and, when available, the signer's name exactly as the file declares it.
- **FR-009**: The feature MUST NOT introduce new third-party dependencies.
- **FR-010**: A failure while looking for signatures MUST NOT cause the upload to fail: the document is processed as if it had no signature, and the existing rejection rules (encrypted, corrupted, no extractable text) are unchanged.
- **FR-011**: When a signature has a non-blank name field for its signer, the note MUST show that name, introduced by "da" (for example "file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00").
- **FR-012**: When a signature has no usable name field but contains the signer's certificate, the note MUST show the name the certificate is issued to, in the same form. The name field takes precedence over the certificate.
- **FR-013**: When the signer's certificate cannot be read or cannot be identified with certainty, the note MUST be shown without a name; a name MUST NOT be guessed, and the name of an issuing authority MUST NOT be shown in place of the signer's.
- **FR-014**: The signer's name MUST be shown as one line of plain text: line breaks and control characters are replaced by spaces, repeated spaces are collapsed and the name is cut at 100 characters, so a name taken from the file cannot change the layout of the note or add text of its own.
- **FR-015**: A failure while reading the signer's name MUST NOT remove the note or fail the upload: the note is shown without a name.

### Key Entities

- **Digital signature note**: A short line added to the extracted text of a signed PDF. It contains the fixed wording "file firmato con firma digitale" and, when available, the signer's name and the date and time the signature declares. One note exists per signature.
- **Signer name**: The name of the person or organisation that signed, as the file declares it, either in the signature's own name field or in the certificate the signer attached. It is reported, not verified.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the signed test PDFs (single and multiple signatures) produce extracted text containing one note per signature.
- **SC-002**: 100% of the unsigned test PDFs produce exactly the same extracted text as before this feature.
- **SC-003**: For a signed PDF, asking whether the document is digitally signed and when gets a correct answer from the local model in all of 3 consecutive runs per question, each answer relying on the note (signed or not signed, and the date and time). For an unsigned PDF, the same question never gets an answer saying it is signed in 3 consecutive runs.
- **SC-004**: No upload that succeeded before this feature fails because of it, across the existing PDF test set.
- **SC-005**: 100% of the signed test PDFs whose signature carries a signer name (in the name field or only in a certificate) produce a note with that name, and asking "who signed this document?" gets that name in all of 3 consecutive runs with the local model.

## Assumptions

- The date and time shown are the ones the signature declares about itself; they are not independently certified (no timestamp-authority check).
- The fixed wording "file firmato con firma digitale" is in Italian and is not translated, regardless of the language of the document or of the question.
- Validity (tampering after signing, certificate trust, revocation) is out of scope. The signer's name is reported as the file declares it, from the signature's name field or from the certificate; it is not checked against any trust list, so it does not prove who really signed.
- The signer's name is read without adding any package: the runtime already provides what is needed to read a certificate (checked in the planning phase; an earlier version of this spec wrongly assumed a new package was required).
- The name appears with the Italian "da" ("by"), like the rest of the note, and is cut at 100 characters.
- A signing date is "readable" when it is in the standard PDF date format and includes at least year, month, day, hour and minute. Anything less, or in another format, counts as no readable date, and the note is then shown without a date. The time zone is shown only when the file declares one.
- Detection applies to PDF only. Word, Excel and text files are unchanged.
- Signed PDFs for the tests are generated by the test itself as fake files; no real user documents are used.
- Existing behavior for scanned, encrypted and corrupted PDFs stays as it is.
