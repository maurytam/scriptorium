# Feature Specification: Model Router (cloud switched off)

**Feature Branch**: `007-model-router`

**Created**: 2026-11-16

**Status**: Draft

**Input**: User description: "Model router for Scriptorium, with the cloud side switched off for now. A router decides which AI backend answers each request: a private document always uses the local model; a task whose complexity is above a threshold would use the cloud model; the user can override with a preference of Auto, Local or Cloud. The cloud backend is off by default (no paid key, the user does not want to spend money for now), so every request goes to the local model. The router, the preference and the way to switch the cloud on later are built and tested, so enabling the cloud later is a configuration step and not a redesign. The existing question flow must keep working unchanged when everything goes to the local model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every question is routed, and a private document never leaves the machine (Priority: P1)

Every question about a document now passes through a routing decision before any model is used. With the cloud switched off,
which is how Scriptorium is delivered, the decision is always the local model, so the person asking sees exactly what they saw
before. The decision rules are already in place, though: a document marked private is answered locally whatever else is asked
of the system, which turns the Private flag, until now only stored, into a real guarantee.

**Why this priority**: it is the whole point of the router and the privacy promise of the product. Without it the Private flag
means nothing, and nothing else in this feature can be trusted.

**Independent Test**: with the delivered settings, ask questions about a private and a non-private document, short and long,
and check that every one is answered by the local model and that the existing question flow behaves exactly as before. Then
check the routing decision directly for every combination of private/non-private, simple/complex and Auto/Local/Cloud.

**Acceptance Scenarios**:

1. **Given** the cloud is off and a ready document, **When** the person asks a question, **Then** the local model answers and
   nothing about the question or the document is sent anywhere else.
2. **Given** a document marked private, **When** a question is asked with any preference and any kind of question, **Then** the
   local model is the only one that can be used.
3. **Given** the cloud is off, **When** the person asks questions with the preference Auto or Local, **Then** every answer comes
   from the local model, with the same limits, notices and error messages as before this feature.
4. **Given** the same inputs (document privacy, preference, complexity), **When** the decision is made twice, **Then** the same
   backend is chosen both times.

---

### User Story 2 - The person can choose Auto, Local or Cloud (Priority: P2)

Before asking, the person can state a preference for the question: **Auto** (let the system decide, the default), **Local**
(always the local model) or **Cloud** (the cloud model). Because the cloud is switched off, the Cloud choice must be honest
about it instead of failing mysteriously.

**Why this priority**: it is the manual override the product promised. It matters less than the privacy rule because, with the
cloud off, Auto and Local give the same result.

**Independent Test**: in the question panel, pick each preference and ask; check that Auto and Local are answered locally and
that Cloud gives the explicit "not available" message while the cloud is off, and is refused for a private document, each with a message the person understands.

**Acceptance Scenarios**:

1. **Given** the question panel, **When** the person does not touch the preference, **Then** Auto is used.
2. **Given** the preference Local, **When** a question is asked, **Then** the local model answers, even for a complex question.
3. **Given** the cloud is off or unreachable, **When** the person chooses Cloud, **Then** no answer is produced and an explicit
   message says that the cloud model is not available; the question is not answered by the local model instead.
4. **Given** a document marked private, **When** the person chooses Cloud, **Then** the choice is refused with a clear message
   saying that a private document is only answered locally; no model is called and the document does not reach the cloud.

---

### User Story 3 - The cloud can be switched on later without redesign (Priority: P3)

When the owner decides to spend money, turning the cloud on is a configuration step: switch it on and provide the key through
the local secret store. From then on, under Auto, a non-private question whose complexity is above the threshold goes to the
cloud, and the Cloud preference works. Until then nothing changes. The routing rules are proven now with a stand-in for the
cloud, so no key and no cost are needed to test them.

**Why this priority**: it protects the investment (no redesign later) but delivers nothing visible today, and it needs the
real cloud backend to exist, even if switched off.

**Independent Test**: with the cloud backend switched on in a test, answering from a stand-in that never reaches the real service, check that a complex non-private question
under Auto goes to it, that a private one never does, that Local never does, and that switching it off again returns every
question to the local model, all without changing any decision rule.

**Acceptance Scenarios**:

1. **Given** the cloud is on and the preference is Auto, **When** a non-private question with complexity above the threshold is
   asked, **Then** the cloud answers it.
2. **Given** the cloud is on and the preference is Auto, **When** a non-private question with complexity at or below the
   threshold is asked, **Then** the local model answers it.
3. **Given** the cloud is on, **When** the document is private, or the preference is Local, **Then** the local model answers.
4. **Given** the cloud is on and the preference is Cloud for a non-private document, **When** a question is asked, **Then** the
   cloud answers it whatever its complexity.
5. **Given** the cloud is switched off again, **When** questions are asked, **Then** all of them go to the local model.

---

### Edge Cases

- A question whose complexity is exactly at the threshold: it is **not** above it, so Auto keeps it local.
- A private document that is also a complex question, with Auto: local. The privacy rule wins over complexity and over the
  Cloud preference.
- The cloud is on but does not answer in time or is unreachable: the person gets a clear message, as the local model's
  unavailability does today. There is no silent fallback to the local model.
- The cloud is switched on but no key is provided: it counts as unavailable, with the same explicit message, and nothing is sent.
- A request that carries no preference, or one that is not Auto, Local or Cloud: no preference means Auto; an unknown value is
  rejected as an invalid request and no model is called.
- The document is not ready, or the question is invalid or too long: the existing messages and limits apply first; routing only
  happens for a question that would otherwise be answered.
- The local model is not running while everything is routed to it: the same "model unavailable" message as today.
- The decision rules must not depend on the content of the document beyond what the complexity measure uses; a private document
  is never inspected for routing in a way that sends it out.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each question about a document MUST be answered by exactly one backend, local or cloud, chosen by one routing
  decision made before any model is called.
- **FR-002**: A document marked private MUST always be answered by the local backend, whatever the preference and the complexity
  of the question.
- **FR-003**: The person MUST be able to state a preference of Auto, Local or Cloud for a question; when none is given, Auto
  applies.
- **FR-004**: With the preference Local, the local backend MUST answer.
- **FR-005**: With the preference Auto, a question whose complexity is above the configured threshold MUST go to the cloud
  backend when it is on and the document is not private; in every other case it MUST go to the local backend.
- **FR-006**: With the preference Cloud and a non-private document, the cloud backend MUST answer when it is on.
- **FR-007**: The cloud backend MUST be off in the delivered settings. While it is off, no question, document text or other data
  MAY be sent to any cloud service, and no paid key is needed to build, test or use the application.
- **FR-008**: When the person chooses Cloud and the cloud is off or unreachable, the application MUST return an explicit message
  saying that the cloud model is not available, MUST NOT answer the question with the local model instead, and MUST NOT fall
  back silently.
- **FR-009**: When the person chooses Cloud for a private document, the application MUST refuse the choice with a clear message
  saying that a private document is only answered locally, MUST NOT call any model for that request, and MUST NOT send the
  document to the cloud.
- **FR-010**: Switching the cloud on MUST need only configuration: the on/off setting, the threshold, and the key supplied
  through the local secret store. It MUST NOT need changes to the routing rules, to the question flow or to the screen.
- **FR-011**: The threshold of complexity above which Auto prefers the cloud MUST be configurable and have a documented
  default.
- **FR-012**: The routing decision MUST be deterministic and explainable: it depends only on the privacy of the document, the
  preference, the complexity of the question and whether the cloud is on, and can be reproduced from those four facts.
- **FR-013**: The local and the cloud backends MUST stay separate: adding, changing or switching off one MUST NOT change how the
  other behaves.
- **FR-014**: The existing question flow MUST be unchanged for the person when every question goes to the local backend: same
  validation, limits, conversation behaviour, cutting notice and failure messages.
- **FR-015**: No key or secret MUST be stored in the repository or in a file committed to it.
- **FR-016**: The preference chosen before a question MUST reach the decision on the server; the screen MUST NOT make the
  routing decision itself.
- **FR-017**: A real cloud backend MUST be part of the delivered application, selectable by the routing decision when the cloud is
  on, and it MUST be off by default. Neither the automated tests nor the delivered settings ever call the real cloud service.

### Key Entities *(include if feature involves data)*

- **Model preference**: what the person asked for on a question: Auto (default), Local or Cloud.
- **Complexity of a question**: a value that says how demanding a question is, compared with the threshold; how it is measured
  is an open point (see Assumptions).
- **Routing decision**: the outcome for one question, local or cloud, produced from the document's privacy, the preference, the
  complexity and whether the cloud is on; it is not stored.
- **Cloud availability setting**: whether the cloud backend is on (off by default), together with its threshold and key,
  supplied through configuration and the local secret store.
- **Document (existing)**: its Private flag, stored since feature 001, now has an effect.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the delivered settings, 100% of questions are answered by the local backend and none sends data outside the
  machine, checked for private and non-private documents and for simple and complex questions.
- **SC-002**: For a private document, 100% of the combinations of preference (Auto, Local, Cloud) and complexity (at, below
  and above the threshold) never use the cloud backend, whether the cloud is on or off.
- **SC-003**: Every automated test and manual check of the question feature (feature 002) that passed before this feature still
  passes without being changed, and a person asking a question with the delivered settings notices no difference.
- **SC-004**: With a stand-in cloud backend switched on in a test, the full table of decisions (privacy × preference ×
  complexity above, at and below the threshold) gives the expected backend in 100% of the cases, without any change to the
  decision rules between runs.
- **SC-005**: A person can pick Auto, Local or Cloud before asking in a single action, and when the choice cannot be honoured
  they read, within the same panel, a message that says why and what they can do.
- **SC-006**: Building, testing and running the application never requires a paid account or key.

## Assumptions

- Single user, running on their own machine. The cloud is off because the project is a personal portfolio with no budget for
  paid services for now; the owner will decide later whether to switch it on.
- "Cloud" means the Claude API, as in the project's description. Nothing in the rules is specific to one cloud provider.
- **Confirmed by the user (2026-11-16), how complexity is measured:** a simple rule that can be explained in a sentence, based
  on the question and the amount of document text it will read, with a single configurable threshold. A smarter measure is
  worthless until summaries and entity extraction exist.
- **Confirmed by the user (2026-11-16), how the answer shows its origin:** a small label in the question panel saying which
  backend answered (for example "Local" or "Cloud"). Until the cloud is on, it would always read Local.
- Conversations stay stateless as today; the preference is sent with each question, not remembered by the server.
- Out of scope: summaries, entity extraction, per-user settings, usage or cost tracking, billing, and any change to what the
  local model reads.
- Depends on the existing question feature (002) and the stored Private flag (001). The cloud backend uses the provider's
  official client library, approved by the user as long as it is free (checked: MIT licence). Which library, and what it pulls
  in, is for the plan to list; nothing else is added without asking.

## Clarifications

### Session 2026-11-16

- Q: What happens when Cloud is chosen but the cloud is off or unreachable? → A: an explicit error and no answer (option A); no silent fallback to the local model. (FR-008)
- Q: What happens when Cloud is chosen for a private document? → A: the choice is refused with a clear message (option A). (FR-009)
- Q: Does the feature include a real cloud backend, or only the point where it will be plugged in? → A: a real cloud backend is included, switched off (option B). The user approved the provider's official client library on the condition that it is free; it was checked on 2026-11-16 and is published by the provider under the MIT licence. Using the library costs nothing: only calls to the service are paid, and none is ever made while the cloud is off. (FR-017)
- Decision by the user: no money is spent for now, so the cloud stays off and no paid key is used. (FR-007, SC-006)
