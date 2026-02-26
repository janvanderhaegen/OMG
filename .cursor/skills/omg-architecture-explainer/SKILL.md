---
name: omg-architecture-explainer
description: Summarizes the current OMG backend architecture and highlights DDD/layering issues to fix.
---

# OMG – Architecture Explainer Skill

## When to use

Use this skill when you need to:

- Understand how the OMG backend is structured.
- Check whether new or existing code respects the `Domain → Application → Infrastructure → Api` layering.
- Spot potential DDD or architectural violations before or after a change.

## How to behave

When invoked, you should:

1. Scan the solution structure and relevant files (e.g., `ARCHITECTURE.md`, domain/application/infrastructure projects).
2. Summarize the current architecture in a few bullet points:
   - Key projects or folders and which layer they belong to.
   - How HTTP endpoints map to application use-cases and domain aggregates.
3. Identify any obvious issues, such as:
   - Domain types depending on infrastructure or API concerns.
   - Business rules implemented only in controllers/endpoints instead of the Domain/Application layers.
4. Suggest small, incremental refactorings that would align the code with:
   - `ARCHITECTURE.md`
   - `.cursor/rules/architecture-ddd.mdc`
   - `.cursor/rules/aspnet-minimal-api.mdc`

Keep the output concise and actionable so it can guide the next Cursor session or pull request.

