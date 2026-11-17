# Feature Specification: Signature Author and Signature Date Buttons

**Feature Branch**: `013-signature-buttons`

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: "Two quick buttons in the ask panel, next to Summarise and Extract entities: 'Signature author' and 'Signature date', so the person does not have to type 'who signed?' and 'when was it signed?' every time. Decisions already taken by the user: the answer is READ DIRECTLY from the digital-signature note that a signed PDF's extracted text already starts with (feature 003-pdf-digital-signature; each note is a line like 'file firmato con firma digitale da Mario Rossi — 2026-03-14 10:32:00 +01:00', the name can be '(firmatario sconosciuto)', the date is optional and the zone only if the file declares it, one line per signature at the start of the text); NO model is called, so no router, no 'Answer with' selector, no Ollama needed, and nothing is sent anywhere; two separate buttons, 'Signature author' and 'Signature date', visible for any ready document; when the document has no digital signature a clear message is shown (for example 'This document has no digital signature.') and nothing is invented; nothing is stored. Defaults to document: only PDF documents can be signed; several signatures are all listed in the order of the file; a signature with no readable date says so; an unknown signer is shown as unknown; the values are shown as the file declares them (the signature is not validated, same as feature 003); a PDF uploaded before feature 003 has no note, so it shows the no-signature message; a text that only quotes the wording but is not a PDF is not a signature. The cloud is not involved and must never be called."

**Scope update, 2026-11-18**: the user pointed out that Word and Excel files can be digitally signed too, and that the feature
must handle them, not only PDFs. Scriptorium reads signatures only from PDFs today (feature 003), so this feature also teaches
the upload to read the signatures of Word (`.docx`) and Excel (`.xlsx`) files and to record them at the start of the text, exactly
as it does for PDFs; the two buttons then work the same on all three types.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Signed Word and Excel files are recognised like signed PDFs (Priority: P1)

When the person uploads a digitally signed Word or Excel file, Scriptorium reads its signatures during the upload and records one
line per signature at the start of the extracted text, in the same form already used for signed PDFs: that the file is digitally
signed, who signed (or unknown) and, when the file declares it, the date and time. The person sees it in the text popup, like
for a PDF. A Word or Excel file that is not signed gets no such line and its text is exactly what it was before.

**Why this priority**: without it the two buttons could only answer for PDFs and would say "no signature" on a signed contract in
Word, which is false. It is the foundation of the other stories for these two types.

**Independent Test**: upload a fake signed Word file and a fake signed Excel file (and an unsigned one of each), open their text and
check that the signed ones start with one line per signature, with the signer and the date as the file declares them, and that the
unsigned ones are unchanged.

**Acceptance Scenarios**:

1. **Given** a Word file with one digital signature that declares a signer and a date, **When** it is uploaded and becomes ready,
   **Then** its text starts with one line naming the signer and the date, followed by a blank line and the rest of the text.
2. **Given** an Excel file with two digital signatures, **When** it is uploaded, **Then** its text starts with two lines, in the
   order of the file.
3. **Given** a Word or Excel file with no digital signature, **When** it is uploaded, **Then** its text is exactly what Scriptorium
   produced before this feature.
4. **Given** a signature with no readable signer or no readable date, **When** the file is uploaded, **Then** the line says
   "unknown signer" or leaves the date out, as for a PDF; nothing is guessed.
5. **Given** a Word or Excel file whose signature part is damaged, **When** it is uploaded, **Then** the upload goes on, the text
   is extracted as before and no line is added (as for a damaged PDF signature).

### User Story 2 - See who signed a document with one click (Priority: P1)

The person opens a ready digitally signed document (a PDF, a Word or an Excel file) and presses **Signature author** in the question panel. Instantly the panel
shows who signed the document, as the file declares it. If the document has several signatures, each signer is listed, in the
order of the file. They do not type a question and nothing waits for a model: the answer is read from the signature information
that Scriptorium keeps at the start of the text of a signed document.

**Why this priority**: it is the first half of the idea. Asking "who signed this?" is a frequent question on contracts and
official documents, and today the person has to type it and wait for a local model that may get it wrong.

**Independent Test**: with a ready signed document of each type (fake ones), press **Signature author** and check that the signer's name appears at
once, exactly as the file declares it, that no model was called (it works with the model server switched off) and that the
other buttons still work as before.

**Acceptance Scenarios**:

1. **Given** a ready signed PDF, Word or Excel file with one digital signature by a named signer, **When** the person presses **Signature author**, **Then**
   the panel shows that name, with a short heading, without any waiting for a model.
2. **Given** a ready signed document with several signatures, **When** the person presses **Signature author**, **Then** every signer is
   listed, one per signature, in the order of the file.
3. **Given** a signature whose signer the file does not name, **When** the person presses **Signature author**, **Then** that
   entry reads as an unknown signer; nothing is guessed.
4. **Given** the model server is not running, **When** the person presses **Signature author** on a signed document, **Then** the
   result appears just the same.
5. **Given** the person has chosen **Cloud** in the **Answer with** selector, **When** they press **Signature author**, **Then**
   the choice makes no difference: nothing is sent to any model and no cloud message appears.

---

### User Story 3 - See when a document was signed with one click (Priority: P1)

The person presses **Signature date** on a signed document and sees when it was signed, as the file declares it: the date and time,
with the time zone when the file gives one. With several signatures each date is shown with the signer it belongs to, so the
dates cannot be mixed up. A signature that declares no readable date says so instead of showing a made-up one.

**Why this priority**: it is the second half of the idea and just as frequent a question. It is separate from the first button
because the person often wants only one of the two, and the user asked for two buttons.

**Independent Test**: with a signed document of each type whose signature has a date, press **Signature date** and check that the date and time
match the file; with one that declares no date, check that the panel says so; with a document with two signatures, check that each
date is next to its signer.

**Acceptance Scenarios**:

1. **Given** a ready signed document whose signature declares a date and time, **When** the person presses **Signature date**, **Then** the
   panel shows that date and time exactly as the file declares them (with the time zone only if the file declares one).
2. **Given** a ready signed document with several signatures, **When** the person presses **Signature date**, **Then** one entry per
   signature is shown, in the order of the file, each with the name of its signer.
3. **Given** a signature with no readable date, **When** the person presses **Signature date**, **Then** that entry says that
   the file declares no signing date.
4. **Given** a signature whose date is in a time zone different from the person's, **When** the date is shown, **Then** it is
   not converted: it appears as the file wrote it.

---

### User Story 4 - A clear answer when there is no signature (Priority: P1)

The two buttons are there for every ready document, so the person can press them on documents that are not signed: an unsigned PDF, Word or
Excel file, a text file, or a PDF, Word or Excel file uploaded before signatures were read. They get a clear message that the document has
no digital signature, never an invented name or date and never an error.

**Why this priority**: because the buttons are always visible, this case is common and the answer must be honest; otherwise the
feature would mislead the person.

**Independent Test**: press each button on an unsigned PDF, on an unsigned Word and Excel file, on a text file and on a text file whose content quotes the wording of
the signature note, and check that all three say that the document has no digital signature.

**Acceptance Scenarios**:

1. **Given** a ready document with no digital signature, **When** the person presses either button, **Then** the panel says
   "This document has no digital signature." and shows no name or date.
2. **Given** a text file, or a Word or Excel file, whose own content contains the same words as the signature note but which has no signature, **When** the
   person presses either button, **Then** it is not taken for a signature and the same message appears.
3. **Given** a PDF, Word or Excel file whose text, further down, contains the same words as a signature note, **When** the person presses
   either button, **Then** only the signatures recorded at the start of the text count; the quoted words do not.
4. **Given** a document uploaded before its signatures were read (so its text has no note), **When** the person presses either button,
   **Then** the no-signature message appears; the person is not told to upload it again, because that is not known.

---

### User Story 5 - The buttons fit the panel and behave like the others (Priority: P2)

The two buttons sit next to **Summarise** and **Extract entities** and look like them. While a request is running the buttons
are disabled, as the others are. The result is a small card above the conversation, never part of the history sent with a
question, it is dropped with **New conversation** or when another document is opened, and pressing the same button again
replaces its own card. The **Answer with** selector is left alone and its choice is ignored by these two buttons.

**Why this priority**: it is polish and consistency; the value is in the first three stories.

**Independent Test**: open a ready document, press each of the four buttons and check that they look alike, that the cards
appear above the conversation, that **New conversation** clears them and that opening another document clears them.

**Acceptance Scenarios**:

1. **Given** the panel is open on a ready document, **When** the person looks at it, **Then** **Signature author** and
   **Signature date** are next to **Summarise** and **Extract entities**, in the same style.
2. **Given** a result card is shown, **When** the person presses **New conversation**, closes the panel or opens another
   document, **Then** the card is gone.
3. **Given** the server cannot be reached, **When** the person presses a button, **Then** they get the same kind of clear message
   as for the other buttons, not a blank panel.
4. **Given** the person presses a button on a document that was deleted meanwhile, **When** the answer comes, **Then** they are
   told the document no longer exists, as for the other buttons.

---

### Edge Cases

- **Several signatures** where some have a name and a date and others do not: each entry is shown on its own terms, in the order
  of the file; none is dropped or merged.
- **A signer name that itself contains a long dash or looks like a date**: the name is shown whole; the date is only what the
  note declares as the date.
- **A very long document**: the signatures are read from the whole stored text, not from the part the model would read, so a long
  PDF still shows them.
- **The document is not ready** (still processing or failed): the person is told that, as for the other buttons; nothing is shown.
- **An empty or damaged note**: nothing is invented; the no-signature message (or "unknown signer" and "no date" for the parts the
  note leaves out).
- **The signature is not validated**: the name and date are what the file says about itself, not proof that the signature is
  valid or the signer is who they claim. A short note in the result says so.
- **Private documents**: the buttons work on them like on any other, since nothing leaves the computer and no model is involved.
- **Pressing twice quickly**: only one request runs at a time; the buttons are disabled while one is running.
- **A signed document whose text could not be extracted**: it is not "ready", so the person gets the not-ready message.
- **A signed Word or Excel file with no text at all** (only the signature): it keeps the rule already used for PDFs for the "no text"
  case; the plan must say what that is, and the signature is still recorded.
- **A document uploaded before this feature** (signed Word or Excel files included) has no recorded signature, so the buttons say it
  has none; uploading it again records it. The person is not told which case it is.
- **Older Word and Excel formats** (`.doc`, `.xls`) are not supported by Scriptorium at all and are out of scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: For every ready document the ask panel MUST offer two buttons, **Signature author** and **Signature date**, next
  to **Summarise** and **Extract entities**, in the same style.
- **FR-002**: Pressing **Signature author** MUST show the signer of each digital signature of the document, in the order of the
  file, with a short heading.
- **FR-003**: Pressing **Signature date** MUST show the signing date and time of each digital signature of the document, in the
  order of the file, each with the name of its signer, exactly as the file declares them (no conversion of the time zone; the
  zone only if the file declares one).
- **FR-004**: A signature with no signer name MUST be shown as an unknown signer, and one with no readable date MUST say that no
  signing date is declared. Nothing MAY be guessed or invented.
- **FR-005**: A document with no digital signature MUST get the message "This document has no digital signature." from either
  button, with no name or date and no error.
- **FR-006**: Only PDF, Word (`.docx`) and Excel (`.xlsx`) documents MUST count as possibly signed. Text that merely contains the
  wording of a signature note, in a text file or further down the text of any document, MUST NOT be taken for a signature; only
  the signatures recorded at the start of the text count.
- **FR-007**: The information MUST come from the document's stored text, from the signature notes already kept there by the
  signature feature; the original file MUST NOT be opened again when a button is pressed, and no document uploaded earlier MUST
  need to be uploaded again to keep working (one uploaded before its signatures were read is shown as having no signature).
- **FR-008**: No language model MUST be called, so neither the model router, nor the **Answer with** selector, nor the local
  model server, nor the cloud MAY be involved; the buttons MUST work with the model server switched off and MUST ignore the
  selector. Nothing MUST leave the computer.
- **FR-009**: The signatures MUST be read from the whole stored text of the document, not from the part a model would read, so
  that a long document shows them.
- **FR-010**: The same refusals as the other buttons MUST apply to a document that is not found (it no longer exists) and to one
  that is not ready (the message says so); a server that cannot be reached MUST give a clear message.
- **FR-011**: The result MUST be shown as a card above the conversation, MUST NOT be part of the history sent with a question,
  and MUST be dropped on **New conversation**, when the panel is closed and when another document is opened. Pressing the same
  button again MUST replace only its own card. The buttons MUST be disabled while a request is running, as the others are.
- **FR-012**: The result MUST say that the name and the date are what the file declares and that the signature has not been
  validated.
- **FR-013**: Nothing MUST be stored: no table, no column, no migration; the answer is produced each time.
- **FR-014**: When a Word or Excel file is uploaded, its digital signatures MUST be read and recorded at the start of its extracted
  text, one line per signature in the order of the file, in the same wording and layout already used for signed PDFs: the signer
  as the file declares it (or unknown), and the date and time only when the file declares them, with the zone only if declared.
  The signature MUST NOT be validated. A Word or Excel file with no signature MUST produce exactly the text it produces today, and
  a damaged signature MUST NOT make the upload fail.
- **FR-015**: The existing behaviour MUST NOT change (apart from the signature lines of FR-014, which add a line to the text of
  signed Word and Excel files, as feature 003 did for PDFs): uploading, the text of signed PDFs, questions, summaries, entities, the
  router and the automated tests of the earlier features keep working with no change to an existing test.
- **FR-016**: No new NuGet or npm package MAY be added, and the real cloud service MUST NOT be called by this feature or its tests.

### Key Entities

- **Signature entry**: one digital signature of a signed PDF, Word or Excel file as the file declares it: its signer (a name, or unknown) and its
  signing date and time (or none), in the order the signatures appear in the file.
- **Signature result**: the entries of one document, or the fact that it has none; produced each time and never stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The person gets the signer, or the date, of a signed PDF, Word or Excel file with one click and no typing, and sees it in under 1
  second, with no waiting for a model.
- **SC-002**: On the signed PDF, Word and Excel files used to check this feature (one signature, several, with and without a name, with and
  without a date, with and without a time zone), 100 % of the names and dates shown match what the note at the start of the text
  says.
- **SC-003**: On documents with no digital signature (an unsigned PDF, Word or Excel file, one from before signatures were read, and
  text files, including a text file that quotes the wording), 100 % of the presses give the no-signature message and 0 give a
  name or a date.
- **SC-004**: With the model server switched off and with **Cloud** chosen in the selector, both buttons still work on 100 % of
  the signed documents.
- **SC-005**: Across all the checks of this feature the model is called 0 times and the real cloud service 0 times.
- **SC-006**: 100 % of the unsigned Word and Excel files used for the checks give the same extracted text as before this feature;
  the existing automated tests pass with the same assertions and no existing test is edited; no new package is added.
- **SC-007**: A person who has never seen the panel finds the two buttons next to the other two and understands their result
  without help.

## Assumptions

- The notes are the ones written by the digital-signature feature (003): one line per signature at the start of the stored text
  of a PDF, in the existing wording and layout, followed by a blank line and then the page text. This feature reads that layout
  and does not change how notes are written.
- The type of a document (PDF, Word, Excel, text) is the one recorded at upload, not decided by its content.
- Word and Excel signatures are the signatures stored inside the file by the Office applications (both the invisible signature and
  the visible signature line). Reading the signer's name from the signer's certificate and the date from what the signature
  declares follows the same rules as for PDFs; how it is read is left to the plan.
- The checks and the automated tests use fake signed Word and Excel files, built for the purpose; no real signed document is used
  and no certificate of a real person is needed.
- Writing the signature lines changes the extracted text of signed Word and Excel files (a line added at the start), as it did for
  signed PDFs in feature 003; questions, summaries and entities of those files will see that line.
- The labels of the buttons and the messages are in English, like the rest of the interface; the note's own wording stays as it is
  in the file (Italian), but the result is shown in English.
- The date is shown in the same form as in the note (`yyyy-MM-dd HH:mm:ss`, with the offset when declared).
- Validating the signature (is it valid, has the document changed) is out of scope, as in the signature feature.
- Checks with the user are done on fake signed files only (PDF, Word, Excel), as in earlier features.
