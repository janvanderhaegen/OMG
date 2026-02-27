# Final Architecture

This document defines the final architecture for the Home Garden
system.\
All structural, technical, and design decisions described below are
intentional and represent the chosen implementation approach.

------------------------------------------------------------------------

# Designing a DDD and Event-Driven Prototype for the Automated Garden Management System

## Executive summary

This report proposes a prototype architecture and implementation plan
tailored to the provided **Automated Garden Management System** backend
case (gardens, plants, humidity simulation, irrigation commands,
reporting, and Docker + tests).

The decided approach:

-   **Model the core business rules with DDD** (clear bounded contexts,
    aggregates that enforce invariants, ubiquitous language). DDD
    prioritizes a rich domain model and explicit boundaries (bounded
    contexts).
-   **Use an event-driven design with a transactional outbox** to
    reliably publish integration events after database commits (avoid
    "dual writes").
-   **Prefer RabbitMQ (or NATS JetStream) over Kafka** for this
    prototype: this still demonstrates messaging, consumers, retries,
    idempotency, and saga-like workflows with **much lower operational
    overhead** than Kafka. RabbitMQ acknowledgements support
    at-least-once delivery semantics.
-   **Use PostgreSQL as the single database** initially, but implement
    **CQRS read models** as separate tables/schemas within PostgreSQL
    (or materialized views) rather than adding a second database---
-   **Use Aspire** (likely ".NET Aspire" / now "Aspire") for local
    orchestration, wiring dependencies, and built-in observability via
    the Aspire dashboard (logs, traces, metrics). Aspire provides
    dev-time orchestration and a dashboard for telemetry.

**Recommended final architecture (prototype-optimized)**\
A small distributed system that showcases DDD + events:

-   **Garden Management API** (write side; DDD aggregates + command
    handlers)\
-   **Irrigation Simulator Worker** (process manager/saga-like watering
    workflow)\
-   **Reporting / Read Model Projector** (consumes events and builds
    query-optimized read models)\
-   **RabbitMQ** as the broker\
-   **PostgreSQL** as the transactional store + read models + outbox\
-   **Aspire AppHost** to run everything locally; plus
    **docker-compose** to satisfy the assignment's explicit requirement
    and to demonstrate portability.
 

## Problem framing and assumptions

### What the provided case requires

The attached case describes a backend-only platform where users manage
gardens and plants with constraints and "bonus" features for irrigation
simulation and reporting. It lists core entities (User, Garden, Plant,
Realtime Plant Metric), CRUD requirements, validation (don't exceed
total garden surface area), REST + Swagger/OpenAPI, Docker +
docker-compose, tests, and documentation.

The irrigation simulation rules are explicit (humidity decay per minute
by plant type, watering duration and humidity increases, start watering
when below ideal humidity).

### Key assumptions (explicit)

Because details are unspecified, this report assumes:
 
-   **Scale**: single-user to small multi-user, not internet-scale;
    correctness and architecture demonstration matter more than
    performance tuning.
-   **UI**: no UI, only REST + OpenAPI/Swagger, matching the
    assignment.\
-   **Messaging requirement**: demonstrate message broker usage and
    event-driven patterns even if the system can be implemented
    synchronously.
-   **Time budget**: 5--10 working days (1--2 weeks).
 

## Domain-driven design strategy and suggested bounded contexts

DDD is fundamentally about building software around a rich domain model
and making boundaries explicit. Bounded contexts divide a large model
into smaller models and make their relationships explicit.

### Ubiquitous language for this domain

-   **Garden**: total surface area; target humidity level (0--100) is
    configured at garden level.
-   **Plant**: type (vegetable/fruit/flower), required surface area,
    ideal humidity, plantation date, belongs to garden.
-   **Humidity reading**: current humidity, timestamps for last
    irrigation start/end.
-   **Watering session**: duration 2 minutes, increases humidity by
    plant type.
-   **Overcrowding**: attempted addition that violates garden surface
    constraint must be rejected with a clear error message.

### Suggested bounded contexts

A pragmatic split (DDD-strong but prototype-friendly):

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
- Owns: Realtime plant metrics, humidity simulation rules, watering
sessions, "commands to mocked irrigation system."\
- Interacts with Garden Management via integration events: Plant
registered/updated/deleted, garden settings changed.

**Reporting Context**\
- Owns: report generation ("watered/unwatered count," watering frequency
per plant, added/deleted plants since date). - Should be built as
projections from events (clean CQRS demo).

A critical DDD practice is **not sharing the same entity model across
contexts**; each context can model "Plant" differently (e.g.,
ReportingPlant vs IrrigationPlantState) and synchronize via events.

### Context map (integration view)

``` mermaid
flowchart LR
  subgraph GardenManagement["Garden Management Context"]
    GM_API["Garden API (Commands)"]
    GM_DB[(Postgres - write model + outbox)]
  end

  subgraph Irrigation["Irrigation & Telemetry Context"]
    IRR_Worker["Irrigation Simulator / Process Manager"]
    IRR_DB[(Postgres - irrigation state)]
    MockIrr["Mock Irrigation System Adapter"]
  end

  subgraph Reporting["Reporting Context"]
    REP_Projector["Projection / Read Model Builder"]
    REP_DB[(Postgres - read models)]
    REP_API["Reporting API (Queries)"]
  end

  Broker["Message Broker (RabbitMQ / NATS / Kafka)"]

  GM_API --> GM_DB
  GM_API --> Broker
  Broker --> IRR_Worker
  Broker --> REP_Projector
  IRR_Worker --> IRR_DB
  IRR_Worker --> MockIrr
  REP_Projector --> REP_DB
  REP_API --> REP_DB
```

This shows bounded contexts, a broker, and separate read
models---visibly "DDD + EDA + CQRS"---without requiring many services.

## Aggregates, entities, commands, events, and process managers

### Aggregate roots and entities

**Garden aggregate (Garden Management Context) --- recommended aggregate
root**\
Why: the case requires enforcing the invariant:\
\> sum(Plants.surfaceAreaRequired) ≤ Garden.totalSurfaceArea

DDD aggregates are explicitly about consistency boundaries; an aggregate
is a cluster of domain objects treated as a unit.

Proposed structure:

-   **Aggregate Root**: `Garden`
    -   `GardenId`\
    -   `TotalSurfaceArea`\
    -   `TargetHumidityLevel` (0--100)\
    -   Collection of `Plant` entities (or at least a summary list that
        includes each plant's required surface area)
-   **Entity**: `Plant` (inside Garden aggregate)
    -   `PlantId`\
    -   `PlantName`, `Species`, `PlantType`, `PlantationDate`\
    -   `SurfaceAreaRequired`\
    -   `IdealHumidityLevel`
-   **Value objects** (good interview signal):
    -   `HumidityLevel` (0--100)\
    -   `SurfaceArea` (m², non-negative)\
    -   possibly `PlantType` enum (vegetable/fruit/flower)

**Irrigation state aggregate (Irrigation & Telemetry Context)**\
I generally do not want frequent telemetry updates to require loading
the entire Garden aggregate. Here, treat irrigation as its own bounded
context with its own aggregate(s):

-   **Aggregate Root**: `PlantHydrationState` keyed by `PlantId`
    -   `CurrentHumidityLevel` (starts at 50%)\
    -   `LastIrrigationStartTime`, `LastIrrigationEndTime`\
    -   `IsWatering` / `ActiveWateringSessionId`
-   **Aggregate Root**: `WateringSession` (process state)
    -   `SessionId`, `PlantId`\
    -   `StartedAt`, `EndsAt` (2 minutes later)\
    -   Status: Planned → Started → Completed → Failed/Cancelled\
    -   This is ideal for demonstrating a saga-like process manager.

**Reporting read model**\
Not an aggregate in the DDD "write invariants" sense; it's a separate
model optimized for queries (CQRS).

### Commands vs events: what to model explicitly

A clean interview structure:

-   **Commands**: intent to change state (validated, can fail).\
-   **Events**: facts that something happened (immutable).

This framing also aligns with event sourcing vocabulary (even when I
don't implement full event sourcing).

**Garden Management commands (examples)** - `CreateGarden` -
`UpdateGardenDetails` / `SetTargetHumidityLevel` - `AddPlantToGarden`
(enforces surface area invariant) - `UpdatePlant` -
`RemovePlantFromGarden`

**Garden Management domain/integration events (examples)** -
`GardenCreated` - `GardenTargetHumidityLevelChanged` -
`PlantAddedToGarden` - `PlantUpdated` - `PlantRemovedFromGarden`

**Irrigation commands/events** - Internal command:
`EvaluatePlantHumidity` (triggered by timer/tick) - NOT: Event:
`PlantHumidityDecreased` (too chatty) - Event: `PlantNeedsWatering` -
Command: `StartWateringPlant` - Event: `WateringStarted` - Command:
`CompleteWateringPlant` - Event: `WateringCompleted` - Event:
`PlantHumidityIncreased`

### Process managers (sagas) in this prototype

The Saga pattern coordinates a multi-step workflow either by
**choreography** (participants react to events) or **orchestration** (a
central orchestrator tells participants what to do).

A process manager (often used interchangeably with saga/orchestrator)
receives a trigger message, tracks process state, and sends commands to
participants.

**Recommended: orchestration-style watering workflow (simple and
demonstrable)**\
- The Irrigation Worker acts as the process manager: - On
`PlantNeedsWatering`, create a `WateringSession` in DB, emit
`WateringStarted`, send command to "Mock Irrigation System." - A
scheduled loop checks for sessions whose `EndsAt <= now` and completes
them, emitting `WateringCompleted`.

This avoids advanced broker features (like delayed messages plugins)
while still demonstrating long-running workflow state and eventual
consistency.

#### Event flow example for watering

``` mermaid
sequenceDiagram
  participant GM as Garden API
  participant DB as Postgres (write + outbox)
  participant MQ as Broker
  participant IRR as Irrigation Process Manager
  participant IRRDB as Postgres (irrigation state)
  participant MOCK as Mock Irrigation Adapter
  participant REP as Reporting Projector

  GM->>DB: AddPlantToGarden (tx)
  DB-->>DB: Persist Plant + Outbox(PlantAdded)
  DB-->>MQ: Publish PlantAdded (outbox dispatcher)
  MQ-->>IRR: PlantAdded
  IRR->>IRRDB: Init PlantHydrationState (humidity=50%)
  IRR-->>MQ: PlantRegisteredForIrrigation
  MQ-->>REP: PlantAdded / PlantRegistered...
  REP->>DB: Update read models

  Note over IRR: Every minute tick (simulated)
  IRR->>IRRDB: Decrease humidity per PlantType
  IRR-->>MQ: PlantNeedsWatering (if below ideal)
  MQ-->>IRR: PlantNeedsWatering
  IRR->>IRRDB: Create WateringSession (EndsAt=+2m)
  IRR->>MOCK: Start watering command
  IRR-->>MQ: WateringStarted
  MQ-->>REP: WateringStarted
  REP->>DB: Update reporting read model

  Note over IRR: Periodic scan finds due sessions
  IRR->>IRRDB: Complete session, increase humidity
  IRR-->>MQ: WateringCompleted
  MQ-->>REP: WateringCompleted
  REP->>DB: Update watering counts/frequency
```

### Reliable event publication: the transactional outbox

A core event-driven systems pitfall is "dual writes" (DB write + broker
publish) where one succeeds and the other fails. The transactional
outbox pattern addresses this by writing outgoing messages to an outbox
table in the same DB transaction and publishing them asynchronously.

By using MassTransit, the system can configure a transactional outbox
so that published/sent messages are stored and then delivered to the
broker after the DB transaction completes.

This is one of the highest-value "senior" signals the system can include
in a prototype because it demonstrates correctness thinking, not just
technology usage.

## Messaging system selection with trade-offs

### What matters most for an interview prototype

Optimizing for:

-   Demonstrating event-driven architecture (pub/sub, consumers,
    retries, idempotency).
-   Keeping operational overhead low enough to finish in 1--2 weeks.
-   Being able to run locally with Docker and/or Aspire orchestration.\
-   Clear documentation of delivery semantics and how I handle
    duplicates.

### Decision recommendation for the prototype RabbitMQ + MassTransit outbox\*\* (for .NET)

Because: - Lowest risk to implement correctly within 1--2 weeks. -
Excellent demo surface: exchanges, queues, routing keys, retries, DLQ,
consumer idempotency, and sagas. - Strong Aspire support for running
RabbitMQ locally. - MassTransit's transactional outbox is documented and
built for this exact reliability problem.

## Database strategy, CQRS, and event sourcing options

### Baseline: single PostgreSQL database (recommended)

This prototype already needs a relational store (Users, Gardens, Plants,
irrigation state, reporting). Using PostgreSQL also integrates cleanly
with Aspire.

**How to still demonstrate CQRS without a second database** CQRS is the
idea of using a different model to update information than the model
used to read it, but it adds complexity and must be used judiciously.

A prototype-friendly implementation: - Keep **write model tables**
normalized to support invariants and transactional updates. - Maintain
**read model tables** (denormalized) updated by consuming events. - Put
both in the same Postgres instance but separate into schemas, e.g.: -
`gm` schema: write model - `read` schema: reporting/read model -
`events` schema: store all events in a table per event category for
additional reporting, store as `jsonb`

### Suggested schema design (write model + outbox + read model)

**Write-side (Garden Management) tables** - `users` - `gardens`
(`garden_id`, `user_id`, `name`, `total_surface_area`,
`target_humidity_level`, concurrency token) - `plants` (`plant_id`,
`garden_id`, `type`, `surface_area_required`, `ideal_humidity_level`,
etc.)

**Irrigation tables** - `plant_hydration_state` (`plant_id`,
`current_humidity`, `last_irrigation_start`, `last_irrigation_end`,
`is_watering`) - `watering_sessions` (`session_id`, `plant_id`,
`started_at`, `ends_at`, `status`)

**Outbox tables** - `outbox_messages` (`message_id`, `occurred_at`,
`type`, `payload`, `headers`, `published_at`, `status`) - Optionally
`inbox_state` if using consumer inbox for exactly-once *processing*
behavior in my consumer boundary (MassTransit supports inbox/outbox
concepts).

**Read-side (Reporting) tables** - `garden_overview_read` (garden
totals: plant count, used area, available area, target humidity) -
`plant_watering_stats_read` (watered count, last watered, watering
frequency counters per period) - `plant_lifecycle_read` (added_at,
deleted_at)

Even if everything stays in a single database, the architectural story
is strong: - "Commands update write model and emit events (outbox)." -
"Events build read models asynchronously (CQRS)."

**Postgres + Redis** For this prototype, Redis is positioned as: a cache for "frequently used API calls" (bonus
requirement)

## Aspire integration, deployment, testing, observability, and security

### What Aspire is and how to use it here

Aspire (formerly ".NET Aspire" and now described as a polyglot platform)
is positioned as a dev-time orchestration tool chain for
building/running distributed applications with integrated observability.
Its "service defaults" help wire telemetry, health checks, and service
discovery. The Aspire dashboard provides views of logs, traces, and
metrics for your application.

For this prototype: - Use an **AppHost** to declare: - Postgres resource
via `AddPostgres` - RabbitMQ resource via `AddRabbitMQ` - My .NET
projects (API, workers) - Use `WithReference` style wiring so connection
strings flow automatically.

### Deployment and infrastructure options

The case explicitly asks for Docker and docker-compose. Docker Compose
defines a multi-container application in a single YAML file and
simplifies running dependencies.

A clean approach: - Provide `docker-compose.yml` for: - Postgres -
RabbitMQ - API + workers - Use Aspire for local dev convenience; keep
docker-compose as the "reviewer-friendly" baseline.

### CI/CD and testing strategy

For an interview prototype, the best signal is layered testing focused
on business rules and integration reliability:

-   **Unit tests (fast)**: domain invariants (surface area), humidity
    calculations, watering transitions.\
-   **Integration tests**: Postgres persistence, outbox publication,
    consumer idempotency.\
-   **End-to-end tests (few but valuable)**: "add plant → humidity drops
    → watering triggered → report updated."

Key is to test the business logic and the "event-driven correctness"
boundary (outbox + consumer).




### Observability

OpenTelemetry is a vendor-neutral observability framework for
generating/collecting/exporting telemetry signals like traces, metrics,
and logs. The OpenTelemetry specs cover signals including logs and
emphasize correlation with shared resource/context.

Aspire's dashboard then becomes the "demo UI": - show a trace where
`AddPlantToGarden` results in event publication and consumption - show
metrics like "waterings completed" - show structured logs correlated by
trace id

The Aspire dashboard explicitly supports viewing logs, traces, and
metrics.

### Security considerations (prototype-appropriate)

The assignment calls for robust login/registration "theoretically"
(bonus), and suggests JWT/OAuth2/third party/cloud-native solutions.

A crisp security story: - Use OAuth 2.0 / OIDC via an external provider
in real life: OAuth 2.0 enables third-party apps to obtain limited
access to HTTP services. - If implementing locally for the prototype: -
Issue JWT access tokens; JWT is a compact, URL-safe means of
representing claims. - Store passwords with a strong hashing approach
and salts (implementation detail). - Authorization: - Gardens are owned
by a User; all garden/plant operations require ownership checks. -
Message security: - Treat broker as internal; still validate schemas and
avoid leaking secrets into message payloads. - Auditability: - Events
provide an audit trail of plant additions/deletions and watering actions
(even without event sourcing).

## Implementation roadmap, minimal viable feature set, and recommended stack

### Minimal viable feature set that still demonstrates DDD + events

The MVP must be intentionally scoped to maximize architectural signal:

**Garden Management (DDD core)** - Create/list/update/delete gardens\
- Add/update/delete plants\
- Enforce surface area invariant with a descriptive error message\
- Emit integration events via outbox: `PlantAdded`, `PlantRemoved`,
`GardenTargetHumidityChanged`

**Irrigation Simulation (EDA + process manager)** - Initialize humidity
at 50% for new plants\
- Every minute tick: decrease humidity by plant type; if below ideal
humidity, start watering\
- Watering lasts 2 minutes and increases humidity by type\
- Emit events: `WateringStarted`, `WateringCompleted`

**Reporting (CQRS read models)** - Expose a report endpoint: - \#
watered plants, \# unwatered plants - watering frequency per plant for
period - plants added/deleted since date\
- Build from events, not from joins in the write model.

**Engineering quality** - OpenAPI/Swagger since the case requires it;
OpenAPI defines a standard interface for HTTP APIs. - Docker compose for
reproducible local run. - Adequate test coverage of business logic.

### Recommended tech stack (C#/.NET path)

This is a "prototype-optimized" stack intended to show expertise without
overengineering:

-   **.NET Web API** (Minimal APIs or Controllers) + OpenAPI generation\
-   **EF Core + PostgreSQL** for write model, outbox, irrigation state,
    read models\
-   **MassTransit** for broker integration and transactional outbox
    support\
-   **RabbitMQ** as broker; exchange/queue model is well documented
-   **Aspire AppHost + ServiceDefaults** for orchestration + telemetry
    wiring
-   **OpenTelemetry** instrumentation surfaced in Aspire dashboard
-   **docker-compose** included regardless (explicit requirement)

### Final recommended architecture with justification

**Final pick: Postgres + RabbitMQ + transactional outbox + CQRS read
models in Postgres + Aspire orchestration**

Justification: - Demonstrates DDD bounded contexts and aggregates (core
interview ask). - Demonstrates event-driven architecture with real
messaging semantics and patterns. RabbitMQ acknowledgement semantics
support at-least-once delivery. - Demonstrates reliability patterns
(transactional outbox) instead of naive "save then publish."\
- Avoids event sourcing complexity while still allowing discussing
event sourcing trade-offs credibly.\
- Keeps ops overhead low enough for a polished 1--2 week deliverable,
while still being "distributed enough" to justify Aspire usage.

### Notes on communicating this in an interview

To maximize impact, README and walkthrough must explicitly call
out:

-   bounded contexts and why they're bounded (language +
    invariants).\
-   aggregate boundary decision (Garden enforcing surface area).
-   eventing strategy: domain events vs integration events; why
    events are facts.\
-   reliability strategy: outbox, idempotent consumers, and how to
    handle at-least-once delivery duplicates.\
-   How Aspire helps me run/debug the distributed prototype and show
    traces/metrics.

Mentioning sources/pattern origins can also help; for example, bounded
contexts and CQRS are widely discussed by Martin Fowler, who also
cautions that CQRS can add risky complexity if overused.

------------------------------------------------------------------------

# Solution Projects and Runtime Components

The solution is structured as a set of focused projects that separate
domain, infrastructure, API hosting, and cross-cutting messaging
contracts, while keeping the runtime surface intentionally small.

## API Host

### OMG.Api

-   Type: ASP.NET Core Minimal API project\
-   Responsibility: Single host that exposes:
    -   Management API (gardens, plants, users)
    -   Telemetrics API (real-time metrics and irrigation simulation
        endpoints)
    -   Reporting/read-model endpoints
-   Notes:
    -   Wires up DI, logging, OpenTelemetry, API versioning, and
        ProblemDetails.
    -   References all Domain and Infrastructure projects.
    -   Hosts message consumers where HTTP surface is required.

## Garden Management Bounded Context

### OMG.Management.Domain

-   Domain model for Garden Management:
    -   Aggregates: Garden, Plant
    -   Value objects: SurfaceArea, HumidityLevel, PlantType
    -   Domain services and domain events

### OMG.Management.Infrastructure

-   EF Core DbContext (PostgreSQL)
-   Repository implementations (if used)
-   Outbox integration for integration events

## Telemetrics / Irrigation Bounded Context

### OMG.Telemetrics.Domain

-   Aggregates: PlantHydrationState, WateringSession
-   Business rules for humidity decrease, watering thresholds
-   Domain events: PlantNeedsWatering, WateringStarted,
    WateringCompleted

### OMG.Telemetrics.Infrastructure

-   EF Core DbContext (PostgreSQL)
-   Message consumers reacting to Management events
-   Adapter for mock irrigation system

## Reporting Bounded Context

### OMG.Reporting.Domain

-   Read-model definitions
-   Projection logic from integration events

### OMG.Reporting.Infrastructure

-   Read-optimized schema in PostgreSQL
-   Consumers updating projections

## Shared Messaging Contracts

### OMG.Messaging.Contracts

-   Shared integration event definitions
-   Shared command message definitions (if needed)
-   Ensures publisher and consumer share strongly-typed contracts

## Optional Shared Kernel

### OMG.SharedKernel

-   Base entities and value objects
-   Result/Outcome types
-   Shared abstractions (IClock, correlation IDs, etc.)

## Aspire AppHost

### OMG.Aspire.Host

-   Orchestrates:
    -   OMG.Api
    -   Worker services (if any)
    -   PostgreSQL
    -   RabbitMQ
-   Dev-time orchestration using Aspire

------------------------------------------------------------------------

# API Design, Versioning, and Error Handling Requirements

## URL Versioning Requirement

All public endpoints MUST be versioned in the URL path using:

    /api/v{version}/...

For this prototype: - Only v1 is supported - All routes are hard-coded
to v1

Example:

    /api/v1/management/gardens/{gardenId}/plants

Future versions must be introduced side-by-side without breaking
existing clients.

------------------------------------------------------------------------

## REST Endpoint Style (Resource-Oriented)

The API intentionally uses traditional resource-oriented REST endpoints
instead of modeling command intent in the URL.

Examples:

-   GET /api/v1/health

-   POST /api/v1/management/gardens

-   GET /api/v1/management/gardens/{gardenId}

-   PUT /api/v1/management/gardens/{gardenId}

-   DELETE /api/v1/management/gardens/{gardenId}

-   POST /api/v1/management/gardens/{gardenId}/plants

-   GET /api/v1/management/gardens/{gardenId}/plants

-   GET /api/v1/management/gardens/{gardenId}/plants/{plantId}

-   PUT /api/v1/management/gardens/{gardenId}/plants/{plantId}

-   DELETE /api/v1/management/gardens/{gardenId}/plants/{plantId}

We explicitly avoid command-style endpoints such as:

    POST /commands/add-plant

Reasoning: - The backend is open and future clients are unknown. - The
assignment language clearly describes CRUD-style functionality. -
Traditional REST semantics are the most appropriate contract for an
externally consumed platform API. - DDD and command concepts remain
internal implementation details.

------------------------------------------------------------------------

## ProblemDetails Requirement

All non-success HTTP responses MUST return RFC 7807-compliant
ProblemDetails payloads.

Each error response includes: - type - title - status - detail -
instance - Optional extensions (errorCode, validationErrors,
correlationId)

Validation failures return structured validation error information in
the extensions collection.

------------------------------------------------------------------------

## No Exceptions for Normal Control Flow

Business and validation failures MUST NOT rely on exceptions.

Instead:

-   Domain methods return explicit Result/Outcome objects.
-   Validation errors are returned as structured error collections.
-   Application layer translates results into HTTP responses.
-   Only unexpected technical failures are handled as exceptions and
    converted into 500 ProblemDetails responses.

Reasoning: - Validation failures are expected behavior in an open
backend. - Explicit results improve clarity and testability. - Avoiding
exception-driven flow ensures predictable API behavior.
