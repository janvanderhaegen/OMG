# OMG - Open Modular Gardening

This repository contains the backend for the Open Modular Gardening case. It is a C#/.NET backend that will expose a RESTful API for managing users, gardens, plants, and (optionally) irrigation simulation and reporting.

The project is built as part of an engineering case and is deliberately focused on clear domain modelling, testable business rules, and a transparent development process using Cursor.

## Built with Cursor

This project is developed using the assistance of Cursor AI agents.

- Cursor-specific rules and skills live under `.cursor/`.
- After each meaningful agentic work session, a markdown summary is written into the `doc/` directory.
- Reviewers can inspect the `doc/` files to understand how Cursor was used throughout the implementation.

## Architecture

This backend will be a **C#/.NET 10** service built on **ASP.NET Core minimal APIs**, following a lightweight, domain-driven modular-monolith design.

- **Runtime & framework**
  - Target framework: `net10.0` for all production and test projects.
  - Hosting model: ASP.NET Core **minimal API** with endpoint groups, typed results, and clear separation between HTTP concerns and domain logic.
- **High-level layers**
  - `Domain`: core aggregates and value objects (e.g., Garden, Plant, HumidityTarget, IrrigationEvent) plus domain services and invariants.
  - `Application`: use-cases and orchestration (e.g., garden CRUD, surface area validation, irrigation simulation), orchestrating domain operations.
  - `Infrastructure`: persistence, external services, and cross-cutting concerns (e.g., EF Core, database, messaging).
  - `Api`: minimal API endpoints, input validation, mapping to/from DTOs, and error handling.
- **Boundaries**
  - Domain does **not** depend on Application, Infrastructure, or Api.
  - Application depends on Domain and abstractions from Infrastructure (e.g., repositories, unit of work) but not concrete persistence types.
  - Api depends on Application and DTOs, but never directly on Infrastructure types.

For full details, see `ARCHITECTURE-FINAL.md`, which documents the final project layout, dependency rules, data flows, and runtime architecture.

## User stories (execution-flow order)

- [X] US01 – Repository setup & Cursor configuration  
As a developer, I want the repository initialized with basic structure, git, Cursor rules, and project-specific skills so that the project is ready for iterative, well-documented development.

- [X] US02 – Architecture design & documentation  
As a developer, I want a clearly documented architecture (domains, layers, boundaries, main components, and data flow) so that implementation is consistent and easy to reason about.

- [X] US03 – Initial API project & health check  
As a developer, I want an initial C#/.NET Web API project with a `/api/v1/health` endpoint so that I can verify the service is running and ready for further features.

- [X] US04 – Containerized local environment (Docker) *(Bonus)*  
As a developer, I can run the backend (API and backing services) locally using Docker and docker-compose so that onboarding and local setup are simple and consistent.

- [ ] US05 – Garden CRUD  
As a user, I can create, view, update, and delete gardens with a name, surface area, and location so that I can organize my physical garden spaces.

- [ ] US06 – Garden overview  
As a user, I can see an overview list of all my gardens linked to my account so that I understand what I am managing.

- [ ] US07 – Garden target humidity configuration  
As a user, I can configure a target humidity level (0–100) per garden so that I can express the desired environment for the plants in that garden.

- [ ] US08 – Plant creation in a garden  
As a user, I can add plants to a garden with their basic details (name, species, type, plantation date, surface area required, ideal humidity level) so that I can track what is growing where.

- [ ] US09 – Plant CRUD  
As a user, I can view, update, and delete plant details in a garden so that I can keep my garden inventory accurate.

- [ ] US10 – Surface area validation  
As a user, I receive a clear error when adding or updating plants in a garden would cause the total `surfaceAreaRequired` to exceed the garden’s `totalSurfaceArea` so that the system prevents overcrowding.

- [ ] US11 – RESTful API for core operations  
As an API consumer, I can interact with a RESTful HTTP API that exposes all garden and plant operations so that I can integrate this system with other tools.

- [X] US12 – Swagger/Scalar/OpenAPI documentation  
As an API consumer, I can discover and test endpoints via a Swagger/Scalar/OpenAPI UI so that I can quickly understand and try the API.

- [ ] US13 – Automated tests for core business rules  
As a developer, I have automated tests for the core business rules (especially surface area validation and any critical invariants) so that changes are safe and regressions are caught early.

- [ ] US14 – Realtime plant metrics model *(Bonus)*  
As a system, I track per-plant realtime metrics (last irrigation times and current humidity level) so that I can reason about watering needs.

- [ ] US15 – Irrigation simulation & control *(Bonus)*  
As a user, I can trigger or observe a simulation that adjusts plant humidity over time according to plant type and issues commands to a mocked irrigation system when plants fall below their ideal humidity level.

- [ ] US16 – Watering activity overview *(Bonus)*  
As a user, I can request a report that shows how many plants were watered vs. not watered in a time window so that I can see if my irrigation strategy is working.

- [ ] US17 – Watering frequency per plant *(Bonus)*  
As a user, I can see how often each plant was watered in a time window so that I can spot outliers.

- [ ] US18 – Change reporting on plants *(Bonus)*  
As a user, I can see how many plants were added or deleted since a chosen date so that I can audit garden changes.

- [ ] US19 – Authentication & login *(Bonus)*  
As a user, I can register and log in securely so that my gardens and plants are protected.

- [ ] US20 – Email verification *(Bonus)*  
As a user, I must confirm my email via a verification code before my account becomes active so that ownership is validated.

- [ ] US21 – Account deletion *(Bonus)*  
As a user, I can delete my account (and associated data, as defined) so that I remain in control of my presence in the system.

- [ ] US22 – Performance optimization of key endpoints *(Bonus)*  
As a developer, I can identify performance-critical endpoints and apply reasonable optimizations (e.g. indexing, simple caching) so that frequently used calls stay fast.

## Getting started

### Run the API directly (development)

From the `src/OMG.Api` directory:

```bash
dotnet run
```

By default, the API will listen on the configured ASP.NET Core URLs. The health endpoint and OpenAPI document are available at:

- Health: `https://localhost:7072/api/v1/health` (or the port configured by ASP.NET Core)
- OpenAPI document: `/openapi/v1.json` 
- Swagger: `/swagger/index.html` 
- Scalar: `/scalar/v1` 

### Run with Docker and docker-compose

From the repository root:

```bash
docker compose up --build
```

This starts:

- `api` (OMG.Api) on `http://localhost:8080`
- `postgres` (PostgreSQL 16) on `localhost:5432`
- `rabbitmq` (RabbitMQ with management UI) on `localhost:5672` and `http://localhost:15672`

Key endpoints when running via Docker:

- Health: `http://localhost:8080/api/v1/health`
- OpenAPI document: `http://localhost:8080/openapi/v1.json`
- Swagger:  `http://localhost:8080/swagger/index.html`
- Scalar:  `http://localhost:8080/scalar/v1`

### Run with Aspire AppHost

From the `src/OMG.Aspire.Host` directory:

```bash
dotnet run
```

The AppHost orchestrates:

- `OMG.Api`
  - Swagger and Scalar are available as custom commands
- PostgreSQL
- RabbitMQ

All new backend code should:

- Target **.NET 10** (`net10.0`).
- Use **ASP.NET Core minimal APIs** for HTTP endpoints.
- Respect the **Domain → Application → Infrastructure → Api** layering described above.


