---
name: omg-test-scaffolding
description: Helps design and scaffold tests for new OMG backend features and user stories.
---

# OMG – Test Scaffolding Skill

## When to use

Use this skill whenever:

- Starting work on a new user story (USxx) or feature.
- Adding or changing a business rule, especially in the Domain or Application layers.
- Creating new HTTP endpoints that should be covered by integration tests.

## How to behave

Given a description of the change (ideally tied to specific user stories), you should:

1. List the key behaviors and invariants that must be tested.
2. Propose concrete test cases, grouped by layer:
   - Domain tests (aggregates, value objects, domain services).
   - Application tests (use-case handlers/services).
   - API/integration tests (minimal API endpoints).
3. Suggest file and type names for the tests that follow common .NET/xUnit conventions.
4. Optionally, scaffold example test code or test method skeletons.

Align your recommendations with `.cursor/rules/testing-strategy.mdc` and ensure tests can be run via `dotnet test` as part of the standard workflow.

