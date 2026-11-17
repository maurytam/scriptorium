# Feature Specification: Docker Compose Setup

**Feature Branch**: `010-docker-compose`

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: "Docker Compose setup of Scriptorium, with an Ollama service. One command (docker compose up --build) starts everything on the user's computer: the API, the web page and a local Ollama server, so the person can use the app without installing .NET, Node or Ollama. Decisions already taken by the user: three services (web, api, ollama); the web page is served by its own container that forwards /api to the API container; the model qwen3.5:4b (about 3 GB) is downloaded automatically at the first start by a start-up service that pulls it only if missing and then stops, models kept in a volume so it is downloaded once; the database and the uploaded files live in Docker-managed volumes that survive docker compose down (removed only with down -v). The cloud stays OFF by default and must never be called for real; no API key or secret is committed (a key, if ever used, comes only from an untracked .env file). The existing way of running without Docker (dotnet run, ng serve, Ollama on the host) keeps working unchanged. No change to the behaviour of the app, no new NuGet or npm package. Document in the README how to start, stop, where the data lives and the note that on a Mac Docker has no GPU so the local model is slower."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start the whole app with one command (Priority: P1)

The person has Docker on their computer and nothing else: no .NET, no Node, no Ollama. From the project folder they run one
command. Scriptorium builds, starts and prints nothing they have to act on. They open one address in the browser and the same
page they know appears, with the upload area and the (empty) document list. They upload a document, wait for it to be ready and
ask a question: the answer comes from the local model, which runs in the same set of containers, and says "Answered on this
computer".

**Why this priority**: it is the whole point of the roadmap item. A person (or a reviewer of the portfolio) who has never set up
the project can try it without installing three toolchains.

**Independent Test**: on a computer with only Docker, run the start command from a clean checkout, open the address in the
README, upload a small fake text file, ask a question and check that an answer comes back labelled "Answered on this computer",
that the summary and the entities buttons work, and that nothing was installed outside Docker.

**Acceptance Scenarios**:

1. **Given** a computer with Docker and a clean checkout, **When** the person runs the single start command, **Then** every part
   of Scriptorium starts and one address in the browser shows the page.
2. **Given** the app is started this way, **When** the person uploads a document and asks a question, presses **Summarise** and
   presses **Extract entities**, **Then** all three work as they do without Docker, answered by the local model, with the same
   messages, labels and limits.
3. **Given** the app is started this way, **When** the person chooses **Cloud** in **Answer with**, **Then** they get the same
   clear message as always (the cloud is off) and nothing is sent anywhere.
4. **Given** the app is running, **When** the person stops it with the documented stop command, **Then** every part stops and
   nothing is left running.

---

### User Story 2 - The model is downloaded once, by itself (Priority: P1)

The local model is large (about 3 GB). The person must not have to download it by hand or know the model's name. At the first
start Scriptorium downloads it if it is missing and then goes on; at every later start it finds the model already there and does
not download it again. While it is downloading the person can see that the app is waiting for the model, not broken.

**Why this priority**: without the model the app starts but cannot answer anything, so the "one command" promise would be false.
It is the slow, failure-prone part of a first start and has to be handled on purpose.

**Independent Test**: on a clean Docker setup, start the app, watch that the model is downloaded once and that questions are
answered after it finishes; stop and start again and check that no new download happens and that the app is ready in a short time.

**Acceptance Scenarios**:

1. **Given** the model is not on the computer, **When** the app is started, **Then** the model is downloaded automatically, the
   progress can be followed in the output of the start command, and the download step ends by itself when the model is there.
2. **Given** the model is already downloaded, **When** the app is started again (also after stopping it), **Then** no download
   happens and the app is usable without waiting for one.
3. **Given** the model is still downloading, **When** the person opens the page and asks a question, **Then** they get the usual
   "the model is not available" message (not a crash or a blank page) and the same question works once the download is over.
4. **Given** the computer is offline at the first start, **When** the download cannot happen, **Then** the output says so in plain
   words and the rest of the app (upload, list, text) still works; asking a question gives the usual "model not available" message.

---

### User Story 3 - My documents and models are still there tomorrow (Priority: P2)

The person stops the app, even removes its containers, and starts it again later: the documents they uploaded, their extracted
text and the downloaded model are still there. Their data is not in the project folder, so it is never committed by mistake. It is
removed only when the person asks for it explicitly, with a documented command.

**Why this priority**: a document tool that forgets its documents at every restart would be unusable, but it is secondary to
being able to start at all.

**Independent Test**: start the app, upload a fake file, stop and remove the containers (without the "remove data" option), start
again and check that the file is still listed and readable and that the model is not downloaded again; then run the documented
"remove everything" command and check that the list is empty and the model is downloaded again at the next start.

**Acceptance Scenarios**:

1. **Given** documents were uploaded, **When** the app is stopped and its containers removed and it is started again, **Then**
   the same documents are listed with the same status, and their text and original files are intact.
2. **Given** the same situation, **When** the app is rebuilt after a code change, **Then** the documents and the model are still
   there.
3. **Given** the person runs the documented command that removes the data, **When** they start the app again, **Then** it starts
   empty, like the first time.
4. **Given** the working folder, **When** the app has run and stored documents, **Then** the working tree has no new file to
   commit (no database, no uploaded document, no model).

---

### User Story 4 - The way without Docker keeps working (Priority: P3)

The person who develops Scriptorium still runs the API with `dotnet run`, the page with `ng serve` and Ollama on the host, and
the tests as before, with no change in their settings, ports or data. Docker is an extra way to run the app, not a replacement.

**Why this priority**: a safety rule for the existing workflow, not new value. It still has to be true and checked.

**Independent Test**: without starting any container, run the API, the page and the tests as described in the README before this
feature and check that everything behaves as before; also check that the Docker files change no setting in use without Docker.

**Acceptance Scenarios**:

1. **Given** no container is running, **When** the person starts the API and the page as before, **Then** they work with the same
   addresses, settings and data folders as before this feature.
2. **Given** the full test suites, **When** they are run, **Then** they pass with the same number of tests as before this feature
   and no test needed a change.
3. **Given** the containers are running, **When** the person also wants to run the development setup, **Then** the README says
   which addresses the containers use, and they differ from the ones of the development setup so the two can run at the same
   time.

---

### Edge Cases

- **A port is already in use** on the computer (for example the development setup is running): the start fails with a message that
  names the port, and the README says how to choose another address; nothing else is left half started.
- **The model server is not ready yet when the API starts:** the API starts anyway and the first questions get the usual "model
  not available" message until the model server answers; it does not need a manual restart.
- **The model download is interrupted** (computer sleeps, connection lost): the next start tries again and does not leave a broken
  half-model that is taken for a good one.
- **A large upload** (up to the limit the app already has, 50 MB) goes through the web container to the API without being refused
  by the forwarder before the API's own limit applies.
- **A slow answer** (a local model on a computer with no GPU can take much longer than on one with a GPU) is not cut by the
  forwarder before the app's own time limit for the model; the person gets the app's own message if that limit is reached.
- **No GPU:** on a Mac, Docker cannot use the graphics chip, so the local model is slower inside Docker than Ollama installed on
  the host; this is said in the README, with the other way (Ollama on the host) as the alternative.
- **Not enough memory or disk** given to Docker for a 3 GB model: the failure shows in the output of the start command and the
  README names the need; the app does not pretend to be ready.
- **Rebuilding after a code change** does not download the model again and does not lose the data.
- **The cloud is off in every case**, including when a key file is present but the option to use the cloud is not switched on
  (the existing rule: both are needed); nothing is sent to the cloud by starting the app.
- **Someone else on the same network** must not reach the app by accident: the app is reachable from the person's own computer
  only, unless they change that on purpose.
- **A secret file** (the optional file with a key) is never committed: it is ignored by git and the README says so.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: One documented command, run from the project folder on a computer with Docker, MUST build and start the whole of
  Scriptorium: the API, the web page and the local model server. No .NET, Node or Ollama installed on the computer MUST be needed.
- **FR-002**: The whole app MUST be reachable at a single address in the browser; the page MUST work as it does today, with the
  same limits, messages and labels, and every feature of the app (upload, list, text, questions, summaries, entities, model
  choice) MUST work through it.
- **FR-003**: There MUST be exactly three kinds of running part: the web page, the API and the local model server; plus a start-up
  step that downloads the model and then ends. The page MUST be served by its own part that forwards the requests of the app to the
  API.
- **FR-004**: The model the app is configured to use (today `qwen3.5:4b`) MUST be downloaded automatically at the start if it is
  missing, only then, and the download step MUST end by itself. The model MUST be kept so that a later start does not download it
  again.
- **FR-005**: While the model is not there (downloading, failed or offline) the rest of the app MUST work and a question, summary
  or extraction MUST give the existing "model not available" message; no part of the app MAY crash or need a manual restart when
  the model arrives.
- **FR-006**: The progress and the failure of the download MUST be visible in the output of the start command, in plain words.
- **FR-007**: The documents' database and the uploaded files MUST be kept outside the containers, in storage managed by Docker,
  so they survive stopping the app, removing its containers and rebuilding it. They MUST be removed only by an explicit, documented
  command.
- **FR-008**: The data (database, uploaded files, downloaded model) MUST NOT be written in the project folder, so nothing generated
  by running the app can be committed by mistake.
- **FR-009**: The cloud model MUST stay off with the delivered settings: no cloud switch on, no key. No part of starting, running or
  testing this feature MAY call the real cloud service, and no key or other secret MAY be committed. A key, if the person ever
  wants one, MUST come only from a file that git ignores, and the README MUST say so.
- **FR-010**: The settings the app already has (model name, timeouts, limits, routing, storage places) MUST stay configurable and
  keep their current default values for the way without Docker; the containers MUST set only what must differ (where the model
  server and the data are) and MUST NOT change the behaviour of the app.
- **FR-011**: Running the app without Docker (the API, the page, Ollama on the host, and the automated tests) MUST keep working
  with the same addresses, settings and data places as before this feature, and the existing automated tests MUST pass unchanged.
- **FR-012**: The web part MUST pass uploads up to the app's own size limit and answers that take as long as the app's own model
  time limit; it MUST NOT cut either earlier than the app does.
- **FR-013**: The addresses (ports) the containers use on the computer MUST differ from the ones of the development setup, so both
  can run together, and the README MUST say how to choose another one if the address is taken.
- **FR-014**: The app MUST be reachable from the person's own computer only by default.
- **FR-015**: No new NuGet or npm package MAY be added; the images MUST be built from the project's own sources and from
  well-known base images.
- **FR-016**: The README MUST explain: what is needed (Docker), how to start, how to stop, how to open the app, where the data
  lives and how to remove it, that the first start downloads about 3 GB and is slow, that on a Mac Docker has no GPU so the local
  model is slower than Ollama on the host (and how to use the host's Ollama instead), how to choose another address, that the cloud
  is off and where a key would go. The roadmap row for this feature MUST become done, and `docs/architecture.md` MUST describe the
  containers.
- **FR-017**: The app inside the containers MUST keep the privacy rule: a private document is processed only by the local model,
  as it is today.

### Key Entities

- **Service**: one of the running parts of the set: the web page, the API, the local model server.
- **Model download step**: a start-up step that makes sure the configured model is on the computer and then ends.
- **Data store**: storage kept by Docker that holds the database, the uploaded files and the downloaded model; it outlives the
  containers and is removed only on purpose.
- **Settings file for secrets**: an optional file, ignored by git, where a key could be put; absent by default.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a computer with only Docker, a person reaches a working page (upload area visible) with one command and one
  address, without installing anything else, in 10 minutes or less at the first start (excluding the model download, which depends
  on the connection) and in 1 minute or less at a later start.
- **SC-002**: At the first start the model is downloaded with no action from the person; at 100 % of later starts, including after
  removing the containers and after a rebuild, it is not downloaded again.
- **SC-003**: After stopping, removing the containers and starting again, 100 % of the uploaded documents are still listed with
  their text and original files; after the documented remove-everything command, none is.
- **SC-004**: With the app started this way, the five things the app does (upload, questions, summary, entities, model choice) each
  work once on fake files and answer with the same messages and labels as without Docker.
- **SC-005**: With Docker not used, the existing test suites (backend and frontend) pass with the same number of tests as before
  this feature and no existing test is edited.
- **SC-006**: Across all the checks of this feature the real cloud service is called 0 times and no secret appears in any
  committed file; after running the app the working tree has 0 new files from the data.
- **SC-007**: A person reading only the README can start, stop, find the data, remove the data and choose another address on the
  first attempt, without asking anyone.

## Assumptions

- The target is one developer's own computer (macOS, also Linux), with a recent Docker that includes Compose; no server, no
  cloud hosting, no multiple users, no certificates (it is plain HTTP on the person's own machine).
- The model and its settings are the ones already in the app's settings; this feature does not choose another model or change how
  the app uses it.
- The addresses are chosen in planning; they only have to differ from 5034 (API) and 4200 (page) of the development setup.
- On a computer where Docker can use a GPU for the model, using it is out of scope here; the default setup uses the processor only.
  Using the host's own Ollama instead of the container's is described in the README as an alternative, not built as a second mode.
- A first start takes long because of the 3 GB download; the time criterion for the first start leaves the download out for that
  reason.
- Backups, data export, upgrading data between versions, running behind a reverse proxy or with a certificate, publishing the images
  to a registry and continuous integration are out of scope.
- Checks with the real model are done with the user on fake files only, as in the previous features; no real documents are used.
- The development-only files of the page (`proxy.conf.json`, `ng serve`) are not used inside the containers and are not changed.
