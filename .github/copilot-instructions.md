## OMG – Copilot Instructions (C#, ASP.NET, DDD, Docker, DevOps)

These instructions guide GitHub Copilot’s behavior for this repository. They are adapted from the Awesome Copilot instructions for C#, ASP.NET REST APIs, DDD/.NET architecture, containerization, and DevOps/CI/CD ([github/awesome-copilot](https://github.com/github/awesome-copilot)).

### C# and .NET

- Prefer modern C# features that keep code clear and expressive.
- Follow consistent naming and organization:
  - PascalCase for types and public members, camelCase for locals and parameters.
  - One main type per file where practical.
- Keep methods focused and small; avoid large “god” classes or methods.

### ASP.NET Core minimal APIs

- Use ASP.NET Core **minimal APIs** for HTTP endpoints.
- Favor resource-oriented routes such as `/api/gardens`, `/api/plants`, `/api/health`.
- Keep endpoint handlers thin:
  - Validate input.
  - Call Application-layer services.
  - Map results to DTOs and HTTP status codes.
- Use clear, consistent DTOs for requests and responses.

### DDD and architecture

- Respect the `Domain → Application → Infrastructure → Api` layering described in `ARCHITECTURE-FINAL.md`.
- Keep domain types free from framework, HTTP, and database concerns where possible.
- Place business rules and invariants in the Domain layer (aggregates, value objects, domain services), not in controllers or endpoints.

### Testing

- Propose tests alongside code changes, especially for new business rules.
- Use xUnit-style tests (or similar) with clear names that describe behavior.
- Cover:
  - Domain invariants (e.g., surface area validation).
  - Application use-cases (e.g., garden and plant workflows).
  - Minimal API endpoints via integration tests where appropriate.

### Docker and runtime orchestration

- When generating Dockerfiles, use multi-stage builds and small runtime images.
- Prefer configuration via environment variables and avoid hard-coded secrets.
- Assume the primary runtime is **.NET 10** (`net10.0`) and the entrypoint is an ASP.NET minimal API project.

### DevOps and CI/CD

- When suggesting CI/CD workflows (e.g., GitHub Actions):
  - Include steps for `dotnet restore`, `dotnet build`, and `dotnet test`.
  - Optionally build and push Docker images in a dedicated job.
  - Use secrets for credentials and connection strings, not hard-coded values.

When in doubt, align suggestions with the patterns and best practices from the Awesome Copilot instructions for:

- ASP.NET REST API Development
- C# Development
- DDD Systems & .NET Guidelines
- Containerization & Docker Best Practices
- DevOps Core Principles
- GitHub Actions CI/CD Best Practices

