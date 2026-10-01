# Feature Specification: Ask Questions About a Document

**Feature Branch**: `002-ask-document-questions`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Ask questions about a document. In the web UI the user picks one of
their uploaded documents (status \"ready\") and asks a question in natural language about its
content; the system answers using the document's extracted text. This is the first AI feature of
Scriptorium and the second item of the roadmap, building on feature 001 (document upload and
parsing). Context: Scriptorium is local-first, and documents carry an IsPrivate flag, introduced
so that sensitive content is only handled by a local model and never sent to a cloud service."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask a question and get an answer (Priority: P1)

A user opens a document that is ready, types a question in their own words, and reads an answer
based on what the document actually says.

**Why this priority**: This is the whole point of the feature and the first moment Scriptorium
turns a stored file into something useful. Without it there is nothing to follow up on.

**Independent Test**: Can be fully tested by uploading a document with a known fact, asking a
question about that fact, and confirming the answer states it correctly.

**Acceptance Scenarios**:

1. **Given** a document with status "ready", **When** the user submits a question about its
   content, **Then** the user sees an answer drawn from that document's text.
2. **Given** the user has just submitted a question, **When** the answer is still being prepared,
   **Then** a visible progress indication is shown and the user cannot submit another question
   until the answer arrives or the attempt fails.
3. **Given** a document whose text does not contain the answer, **When** the user asks about it,
   **Then** the system says that the document does not say, instead of inventing an answer.
4. **Given** a document that is still processing or has failed, **When** the user tries to ask a
   question, **Then** the question is not accepted and the user is told why.

---

### User Story 2 - Ask follow-up questions (Priority: P2)

After the first answer, the user asks further questions that depend on what was already said
("and the second point?", "who signed it?") without repeating the context.

**Why this priority**: Real reading is a back-and-forth. It makes the feature pleasant, but a
single question and answer already delivers value on its own.

**Independent Test**: Can be fully tested by asking a question, then a short follow-up that only
makes sense given the first answer, and confirming the follow-up is answered correctly.

**Acceptance Scenarios**:

1. **Given** a question and its answer are on screen, **When** the user asks a follow-up that
   refers to them, **Then** the answer takes the earlier exchange into account.
2. **Given** a conversation about one document, **When** the user starts a new conversation or
   opens a different document, **Then** the new conversation begins empty and does not carry over
   anything from the previous one.
3. **Given** a conversation is on screen, **When** the user reloads or leaves the page, **Then**
   the conversation is gone and nothing about it has been saved.

---

### User Story 3 - Clear limits and failures (Priority: P3)

The user always understands what happened when the system cannot answer fully: the document is too
long to be read in full, the answering model is not available, or the answer takes too long.

**Why this priority**: Trust depends on honest behaviour at the edges, but the main flow works
without it.

**Independent Test**: Can be fully tested by asking about a very long document, then with the
answering model switched off, and confirming the messages are specific and the app stays usable.

**Acceptance Scenarios**:

1. **Given** a document whose text is longer than can be read at once, **When** the user asks a
   question, **Then** the answer is given from the beginning of the document and shows a clear
   notice that it is based only on the first part.
2. **Given** the answering model is not available, **When** the user asks a question, **Then** a
   specific, actionable message is shown, the question stays in the input and the user can retry.
3. **Given** an answer takes unusually long, **When** the waiting time passes a reasonable limit,
   **Then** the attempt ends with a clear message and the user can try again.
4. **Given** an empty question or one that is too long, **When** the user submits it, **Then** it
   is not sent and the user is told what to fix.

---

### Edge Cases

- What happens when the document is deleted while its conversation is open? Further questions are
  refused with a clear message saying the document no longer exists.
- What happens when the user asks a question in a different language from the document? The
  question is answered, in the language of the question.
- What happens when the user submits questions in quick succession? Only one question is handled at
  a time; the next one can be sent after the current one ends.
- What happens when the question has nothing to do with the document? The system says the document
  does not cover it, rather than answering from general knowledge.
- What happens to the rest of the application while an answer is being prepared? Uploading,
  browsing and deleting documents keep working normally.
- What happens when the document has a very short text (a few words)? The question is answered from
  what is there, or the system says the document does not say.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to choose one of their uploaded documents and ask a question about
  it, in natural language, from the web UI.
- **FR-002**: System MUST accept questions only for documents with status "ready", and MUST tell
  the user why a question is refused for a document that is processing, failed or no longer exists.
- **FR-003**: System MUST answer from the document's extracted text, and MUST say that the document
  does not contain the answer when it does not, instead of presenting outside knowledge as if it
  came from the document.
- **FR-004**: System MUST answer using a model running on the user's own computer. The text of a
  document and the user's questions MUST NOT be sent to any external service, for any document,
  whether or not it is marked private.
- **FR-005**: System MUST show a visible progress indication from the moment a question is submitted
  until the answer is shown or the attempt fails, and MUST handle one question at a time.
- **FR-006**: System MUST interpret a follow-up question in the context of the earlier questions
  and answers of the same conversation about the same document.
- **FR-007**: System MUST keep a conversation only while the page is open: starting a new
  conversation, opening another document or reloading MUST begin with an empty conversation, and
  nothing about the conversation MUST be stored afterwards.
- **FR-008**: When the document text is longer than can be read at once, System MUST answer from the
  beginning of the document and MUST show a clear notice, together with the answer, that it is based
  only on part of the document.
- **FR-009**: System MUST show a specific, actionable message when the answering model is not
  available, when an answer takes too long, or when an unexpected error occurs; it MUST keep the
  user's question in the input so it can be retried, and MUST remain usable.
- **FR-010**: System MUST reject an empty question and a question longer than the allowed length,
  with a message that says what to correct.
- **FR-011**: System MUST keep the rest of the application usable (uploading, listing and deleting
  documents) while an answer is being prepared.
- **FR-012**: System MUST answer in the language of the question.

### Key Entities *(include if feature involves data)*

- **Question**: What the user asks, in natural language, about one specific document.
- **Answer**: The system's reply to a question, based on the document's text, possibly accompanied by
  a notice that only part of the document was used or that the document does not contain the answer.
- **Conversation**: The ordered exchange of questions and answers about one document during one visit
  to the page. It is not stored: it exists only while the page is open.
- **Document** (existing, from feature 001): Its extracted text is the only source of the answers; its
  status decides whether it can be questioned, and its private flag is respected because no content
  ever leaves the machine.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a typical document (up to about 20 pages), 90% of questions show a visible "working"
  indication within 1 second and display their answer within 60 seconds on an ordinary personal
  computer.
- **SC-002**: For 100% of questions, on documents both private and not private, no document text or
  question leaves the user's computer (verifiable by watching the machine's network traffic during
  a test session).
- **SC-003**: On a reference set of questions whose answers are not in the document, at least 90% are
  answered by saying that the document does not contain the answer.
- **SC-004**: 100% of failures (model unavailable, timeout, document no longer available) end with a
  specific message and let the user retry without reloading the page, with zero crashes.
- **SC-005**: A user can have a conversation of at least 10 question-and-answer exchanges about a
  document without having to restate the context.
- **SC-006**: While an answer is being prepared, a user can still upload, list and delete documents
  without noticeable slowdown.

## Assumptions

- Scriptorium remains single-user and local-first: no authentication and no multiple accounts.
- A conversation concerns exactly one document; asking one question across several documents is out
  of scope.
- Only documents with status "ready" can be questioned, and the extracted text from feature 001 is
  the sole source of answers.
- A model able to run on the user's computer is installed and running; installing and starting it is
  the user's responsibility and outside the application. If it is not available the application says
  so (FR-009).
- Answers are produced only by that local model. Cloud models, and the rules that choose between a
  local and a cloud model, belong to the separate "Model router" feature on the roadmap; the private
  flag therefore needs no special handling here, since nothing is ever sent out.
- The maximum question length is 2,000 characters.
- How much of a document can be read at once depends on the model in use; beyond that, only the
  beginning is used and the user is told (FR-008). Finding the relevant passages in very long
  documents is not part of this feature.
- The quality of an answer depends on the model; the application does not guarantee that answers are
  correct, only that they come from the document's text and say so when the text does not cover the
  question.
- Out of scope: summaries, entity extraction, saving or exporting conversations, quoting the passages
  an answer is based on, and asking questions about several documents at once.
