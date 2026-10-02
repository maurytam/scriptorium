Project Overview
Scriptorium is a personal AI-powered document intelligence tool built in .NET 
10.
It lets the user load documents (PDF, Word, Excel, TXT), ask questions, get summaries,
and extract key entities — routing each request to either a local Ollama model (for
private/sensitive documents) or the Claude API (for complex reasoning tasks).
Target user: single developer, local-first, web use on macOS.
No authentication, no multi-tenancy, no cloud storage in v1.0.

Tech Stack

Runtime: .NET 10 / C# 14
Frontend: Angular
Backend: ASP.NET Core 10 minimal API
AI - Local: OllamaSharp → http://localhost:11434 (model: qwen3.5:4b)
AI - Cloud: Anthropic .NET SDK (claude-sonnet-4-20250514)
ORM: Entity Framework Core 10
Patterns: Clean Architecture, Repository pattern
Document parsing: PdfPig (PDF), DocumentFormat.OpenXml (Word/Excel)
Containerization: Docker + docker-compose (includes Ollama service)


Project Structure
Scriptorium/
├── src/
│   ├── Scriptorium.Core/            # Entities, interfaces, DTOs, domain logic
│   ├── Scriptorium.Infrastructure/  # Ollama, Claude, SQLite, file parsers
│   ├── Scriptorium.Web/             # Angular UI
│   └── Scriptorium.API/             # ASP.NET Core minimal API endpoints
├── tests/
│   ├── Scriptorium.Core.Tests/
│   └── Scriptorium.Infrastructure.Tests/
├── docs/
│   └── architecture.md
├── docker-compose.yml
├── CLAUDE.md
└── README.md

Build & Run Commands
bash# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run the web app (development)
dotnet run --project src/Scriptorium.Web

# Run with Docker (includes Ollama)
docker-compose up --build

# EF Core migrations
dotnet ef migrations add <Name> --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API
dotnet ef database update --project src/Scriptorium.Infrastructure --startup-project src/Scriptorium.API

Architecture & Key Patterns
Model Router
The ModelRouter class in Scriptorium.Core decides which AI backend to use:

If document is flagged IsPrivate = true → always use Ollama
If task complexity score > threshold → use Claude API
User can override manually via ModelPreference enum (Auto / Local / Cloud)

Interfaces (Core — no dependencies on infrastructure)

IAIProvider — implemented by OllamaProvider and ClaudeProvider
IDocumentParser — implemented per file type
IDocumentRepository, IConversationRepository

Coding Conventions

Naming: PascalCase classes, camelCase locals, _camelCase private fields
Async: always async/await, never .Result or .Wait()
Nullability: nullable reference types enabled — no ! suppression without comment
Error handling: Result<T> pattern in Core, no raw exceptions across layer boundaries
DI: constructor injection only — no service locator
Tests: xUnit + FluentAssertions + Moq; one test class per production class


Do Not

Do not add NuGet packages without asking first
Do not use dynamic or reflection unless strictly necessary
Do not put business logic in Blazor components — keep them as thin views
Do not call HttpClient directly — always go through the provider abstractions
Do not commit connection strings or API keys — use dotnet user-secrets locally
Do not mix Ollama and Claude logic in the same class
Do not commit to the main branch — use feature branches and PRs
Do not commit automatically without asking first

Environment & Secrets (local dev)
bashdotnet user-secrets set "Anthropic:ApiKey" "<your-key>" --project src/Scriptorium.API
dotnet user-secrets set "Ollama:BaseUrl" "http://localhost:11434" --project src/Scriptorium.API
dotnet user-secrets set "Ollama:Model" "qwen3.5:4b" --project src/Scriptorium.API


Preferred Working Style

Propose the approach before writing code for non-trivial changes
Write the interface before the implementation
Keep methods under 30 lines — extract if longer
One responsibility per class, and in general, applie SOLID principles, when possible
When in doubt, ask — don't guess business logic