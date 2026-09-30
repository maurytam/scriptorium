<!--
Sync Impact Report
Version change: 1.0.0 (provisional/inferred) → 1.0.0 (finalized ratification)
Modified principles:
  - Restructured from 6 consolidated principles to 8 atomic principles, per explicit user-supplied
    content (previously: I. Clean Architecture & Layer Isolation, II. Provider Abstraction,
    III. Async-First & Null Safety, IV. Explicit Error Handling, V. Test Discipline,
    VI. Simplicity/SOLID/Controlled Dependencies → now: I–VIII below, splitting III into
    standalone Async-First and Nullable Safety principles, and splitting VI into standalone
    Constructor Injection, Single Responsibility/Simplicity, and Provider Isolation principles).
Added sections: none new (Non-Negotiable Constraints and Development Workflow retained/refined)
Removed sections: none
Follow-up TODOs:
  - RATIFICATION_DATE TODO from the provisional draft is now resolved: this explicit,
    user-authored submission is treated as the formal ratification, dated 2026-08-03.
-->

# Scriptorium Constitution

## Core Principles

### I. Clean Architecture
`Scriptorium.Core` has zero dependencies on `Scriptorium.Infrastructure`. All AI and data
access from Core MUST go through interfaces defined in Core (`IAIProvider`,
`IDocumentParser`, `IDocumentRepository`, `IConversationRepository`); concrete
implementations live only in Infrastructure. Rationale: keeps the domain testable and
framework-independent as the single point of truth for business rules.

### II. Async-First
All I/O-bound and AI-provider code MUST use `async`/`await`. `.Result` and `.Wait()` are
forbidden anywhere in the solution. Rationale: prevents deadlocks and thread-pool
starvation, especially under concurrent document/chat requests.

### III. Nullable Reference Safety
Nullable reference types are enabled solution-wide. The null-forgiving operator (`!`) MUST
NOT be used without an inline comment explaining why the compiler's null-analysis does not
apply in that specific case. Rationale: makes null-safety violations a deliberate,
reviewable exception rather than a silent suppression.

### IV. Explicit Error Handling (Result<T>)
Errors flow through `Result<T>` in Core. Raw exceptions MUST NOT cross layer boundaries
(Infrastructure → Core → API) as the primary control-flow mechanism. Rationale: failure
modes must be visible in method signatures, not hidden in try/catch blocks scattered
across callers.

### V. Constructor Injection Only
Dependency injection is constructor-only. The service locator pattern MUST NOT be used
anywhere in the solution. Rationale: keeps dependencies explicit and testable; service
locators hide a class's real requirements.

### VI. Single Responsibility & Simplicity
Each class has one responsibility. Methods MUST stay under 30 lines — extract when longer.
Apply SOLID principles where practical, not as ceremony. Rationale: with a single developer
and no code-review safety net, small and focused units are the primary defense against
unmanageable complexity.

### VII. Provider Isolation
Ollama and Claude logic MUST NEVER be mixed in the same class. Routing between local and
cloud models is decided only in `ModelRouter` (private documents → Ollama; complexity above
threshold → Claude; explicit user override via `ModelPreference`). No code outside a
provider implementation may call `HttpClient` directly — all AI calls go through the
`IAIProvider` abstraction. Rationale: centralizes the privacy-sensitive routing decision in
one auditable place and stops provider-specific quirks leaking into unrelated code.

### VIII. Test Parity
Every production class has a matching test class, written with xUnit, FluentAssertions,
and Moq. Rationale: tests are the primary regression defense in a codebase with no
dedicated QA process.

## Non-Negotiable Constraints

- No new NuGet packages without explicit approval first.
- No `dynamic` or reflection unless strictly necessary.
- No business logic in view components — keep them thin.
- No direct `HttpClient` calls — always through provider abstractions (Principle VII).
- Never commit secrets or connection strings — use `dotnet user-secrets`.
- Never commit directly to `main` — feature branches and PRs only.
- Never commit automatically without asking first.

## Development Workflow

- Propose the approach before writing code for non-trivial changes.
- Write the interface before the implementation.
- When business logic is ambiguous, ask — don't guess.
- EF Core schema changes go through migrations
  (`dotnet ef migrations add <Name> --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API`),
  never manual schema edits.

## Governance

This constitution supersedes conflicting guidance in `CLAUDE.md` or ad-hoc conventions;
where `CLAUDE.md` provides operational detail (build commands, directory layout, secret
setup) it remains the operational reference, but where the two conflict on a rule, this
document wins. Amendments are made by editing this file directly, incrementing
**Version** per semantic versioning (MAJOR: incompatible principle removal/redefinition;
MINOR: new principle or materially expanded guidance; PATCH: wording/clarification only),
updating **Last Amended**, and recording the change in a Sync Impact Report comment at the
top of this file. All PRs and self-reviews MUST verify compliance with the Core Principles
above; any deviation must be justified in the PR description or flagged to the user before
merging. Complexity (new abstractions, new dependencies, deviations from the 30-line
method guideline) must be justified against Principle VI before being accepted.

**Version**: 1.0.0 | **Ratified**: 2026-08-03 | **Last Amended**: 2026-08-03
