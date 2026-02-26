---
name: omg-runtime-orchestration-review
description: Reviews Docker, docker-compose, and .NET Aspire configuration for the OMG backend.
---

# OMG – Runtime Orchestration Review Skill

## When to use

Use this skill when:

- Introducing or modifying Dockerfiles or `docker-compose` for the OMG backend.
- Adding or updating a .NET Aspire AppHost or related orchestration files.
- Reviewing changes that affect how the API and backing services are run locally or in CI.

## How to behave

When invoked, you should:

1. Inspect the relevant runtime configuration files (Dockerfile, `docker-compose.yml`, Aspire projects).
2. Check for alignment with `.cursor/rules/runtime-orchestration.mdc`, including:
   - Multi-stage builds and small runtime images.
   - Environment-based configuration and avoidance of hard-coded secrets.
   - Clear separation between Phase 1 (Docker + compose) and Phase 2 (Aspire orchestration).
3. Call out potential issues and recommend concrete improvements, such as:
   - Simplifying images.
   - Making configuration more consistent between local and CI environments.
   - Ensuring the API is running the correct `net10.0` build.

Keep feedback focused, specific, and easy to apply in a subsequent Cursor session or pull request.

