---
name: note-a-session-logging
description: Generates concise markdown summaries of Cursor agentic work sessions and saves them into the doc/ directory. Use after completing a meaningful unit of work on the Automated Garden backend.
---

# OMG - Open Modular Gardening – Session Logging

## When to use this skill

Use this skill **immediately after** completing a meaningful unit of work, such as:

- Implementing or refining a user story.
- Performing a non-trivial refactor.
- Creating or updating documentation.
- Running a finalize/commit workflow.

Each time, create a new markdown file in `doc/` that summarizes the session.

## File naming convention

- Place files in the `doc/` directory.
- Use the pattern: `YYYY-MM-DD-session-XXX.md`
  - `YYYY-MM-DD`: current date.
  - `XXX`: three-digit counter for that day starting at `001` (e.g. `2026-02-26-session-001.md`).

## Required structure

Every session log must follow this structure:

```markdown
# OMG - Open Modular Gardening – Session Log

- Date: YYYY-MM-DD
- Time: HH:MM (local time)
- Related user stories: USxx, USyy (or "N/A")

## Context
Short, 2–4 sentence description of what this session focused on.

## What was done
- Bullet list of key changes, decisions, and created/modified files.
- Prefer 3–8 bullets, each 1–2 lines.

## Design decisions
- List non-obvious choices, trade-offs, or constraints that shaped the solution.
- Include assumptions that might need revisiting later.

## Content expectations

- Be **concise and information-dense**; avoid fluff.
- Emphasize:
  - Why changes were made.
  - How they relate to specific user stories (US01–US22).
  - What should happen next.
- Do **not** paste long code snippets or stack traces; summarize them instead.

