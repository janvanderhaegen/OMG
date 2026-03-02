# Architecture

This document defines the final architecture for the Open Management system for Gardens.\
All structural, technical, and design decisions described below are
intentional and represent the chosen implementation approach.

------------------------------------------------------------------------

# Designing a DDD and Event-Driven Prototype for the Open Management system for Gardens

## Executive summary

This report proposes a prototype architecture and implementation plan
tailored to the provided **Open Management system for Gardens** backend
case (gardens, plants, humidity simulation, irrigation commands,
reporting, and Docker + tests).

The decided approach:

-   **Model the core business rules with DDD** (clear bounded contexts,
    aggregates that enforce invariants, ubiquitous language). DDD
    prioritizes a rich domain model and explicit boundaries (bounded
    contexts). We have two domains: 
    - Garden management
    - Plant Irrigation & Telemetry
-   **Use an event-driven design with a transactional outbox** to
    reliably publish integration events after database commits (avoid
    "dual writes"): RabbitMQ + MassTransit 
-   **Use PostgreSQL as the single database** initially, but we have a schema for each domain. 
-   **Dashboards** Aspire provides dev-time orchestration and a dashboard for telemetry. Consume via openAPI (import in PostMan) and added scalar documentation. (Swagger also implemented as it was an explicit requirement)

**Final architecture (prototype-optimized)**\
A small distributed system that showcases DDD + events:

-   **Garden Management API** 
    - read side: *performant* direct SQL queries
    - write side: DDD aggregates 
        - structural validation (datetime parsing, etc) is done in the minimal API endpoints, business validation done in the domain objects
        - actions publish domain events. Some domein events are translated to messages on RabbitMQ
-   **Irrigation Simulator Worker** 
        - Background Service calculates hunmidity based on watering decay rules to generate fake reader data
        - Webhook call is simulated to mock incoming reader data, this operates on DDD aggregates
        - DDD events, translated in RabbitMQ messages for WateringRequired and HydrationSatisfied, the consumer translates these into calls to start or stop watering
-   **Reporting / Read Model Projector** 
    - currently: simple and optimized queries on both DB schemas
    - but could be a separate service that operates on a denormalized version of the database for fast queries
        - can update nightly from the original database
        - OR: consume RabbitMQ events and build a new context based on the events (but: could get out of sync due to subtle bugs resulting in hard migrations)
    - or a combination: read today's data live and history from denormalized reporting DB 
-   **RabbitMQ** as the broker\
-   **PostgreSQL** as the transactional store + read models + outbox pattern (MassTransit)
-   **Aspire AppHost** to run everything locally; plus
    **docker-compose** to satisfy the assignment's explicit requirement
    and to demonstrate portability.
-   **Postgres + Redis** For this prototype, Redis isn't yet implemented, but could be positioned to implement a cache for "frequently used API calls". Management and reporting channels can use reddis for output caching (built-in minimal API functionality) and on the reads and domain entity caching on the writes.

 

## Problem framing and assumptions

### What the provided case requires

The attached case describes a backend-only platform where users manage
gardens and plants with constraints and "bonus" features for irrigation
simulation and reporting.

The irrigation simulation rules are explicit (humidity decay per minute
by plant type, watering duration and humidity increases, start watering
when below ideal humidity) and the entire irrigation system can be mocked.

### Key assumptions (explicit)

Because details are unspecified, this report assumes:
 
-   **Scale**: single-user to own  to small multi-user, not internet-scale;
    correctness and architecture demonstration matter more than
    performance tuning.
-   **UI**: no UI, only REST + OpenAPI/Swagger/Scalar, matching the
    assignment.
-   **Messaging requirement**: demonstrate message broker usage and
    event-driven patterns even if the system can be implemented
    synchronously.
-   **Migrations**: db migrations are set to automatically update the database for development

## API design

### Resource centric endpoints
The specs explicitly called for endpoints to "Create", "Read", "Update", "Delete".

Even though we have DDD with specific methods as "RenamePlant" or "AdjustIdealHumidity", the management endpoints do not reflect a business intent. 
This might actually be appropriate for an "Open" system where the intent isn't always known or the ubiquitous domain language isn't always the same between caller and server.  I have seen this used in production systems: Zendesk API has (for example) the ability to POST an update to a helpdesk document, the "history" shows domain events like "Draft Published" or "Translation Added".

### Versioning
Added v1 in the URL schema, to provide options for later versioning without breaking the existing URLs.

### No Exceptions for Normal Control Flow

Business and validation failures MUST NOT rely on exceptions.

Instead:

-   Domain methods return explicit Result/Outcome objects.
-   Validation errors are returned as structured error collections.
-   Application layer translates results into HTTP responses.
-   Only unexpected technical failures are handled as exceptions and
    converted into 500 ProblemDetails responses.

Reasoning: exceptions are magnitude slower than result parttner, frequent validation failures are expected behavior in an open
backend
 

## Domain-driven design strategy and bounded contexts

DDD is fundamentally about building software around a rich domain model
and making boundaries explicit. Bounded contexts divide a large model
into smaller models and make their relationships explicit.
 
### Bounded contexts

A pragmatic split:

**Identity & Access Context**\
- Owns: User registration/authentication concepts, email verification
(bonus).\
- Likely implemented as: minimal in-app user table + password hashing,
or "theoretical" external IdP (OIDC/OAuth2) integration.

**Garden Management Context (core write model)**\
- Owns: Gardens, Plants, the surface area invariant, target humidity
configuration. - This is the main bounded context where I'll showcase
aggregates and invariants.

**Irrigation & Telemetry Context**\
- Owns: Realtime plant metrics, watering
sessions, "commands to mocked irrigation system."\
- Interacts with Garden Management via integration events: Plant
registered/updated/deleted, garden settings changed.

**Reporting Context**\
- Not really a DDD (read-only)

A critical DDD practice is **not sharing the same entity model across
contexts**; each context can model "Plant" differently (e.g.,
ReportingPlant vs IrrigationPlantState) and synchronize via events. This is to avoid "god models".
      


## Deployment, testing, observability, and security

### What Aspire is and how to use it here

Aspire is a dev-time orchestration tool chain for
building/running distributed applications with integrated observability.
Its "service defaults" help wire telemetry, health checks, and service
discovery. The Aspire dashboard provides views of logs, traces, and
metrics for the application.

### Deployment and infrastructure options

The case explicitly asks for Docker and docker-compose. Docker Compose
defines a multi-container application in a single YAML file and
simplifies running dependencies.

Using Aspire for local dev convenience; 


### CI/CD and testing strategy
Tests were added on all domain entities X methods for most outcomes.
Some tests were added on the endpoints.

 
### Security considerations (prototype-appropriate)
The server uses ASP.NET Core Identity with username and password (storing with strong hashing & salts), sends JWT access/refresh tokens, a verification code for email verification (a message is sent via RabbitMQ but no handler to send it to an email service), and securing garden/plant/telemetrics endpoints per user.

Authorization: - Gardens are owned by a User; all garden/plant operations require ownership checks.

Message security: - Treat broker as internal; still validate schemas and
avoid leaking secrets into message payloads. -

Auditability: - Events can be logged to provide an audit trail of plant additions/deletions and watering actions
(even without event sourcing).

Soft deletion strategy;
  - Note: when account is deleted, the server first obfuscates the user's personal data from the auth schema & send events via RabbitMQ. These events could then be used in sub-systems to remove Irrigation&Telemetrics data, reporting data, audit trails, etc.

Auth endpoints do not expose internal state (user does not exist, etc) and are rate-limited.
       
