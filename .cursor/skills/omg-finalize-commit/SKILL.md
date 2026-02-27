---
name: note-a-finalize-commit
description: When the user asks to finalize or commit work in this project, generate a concise commit message from conversation context, run tests, and if they pass, commit and push the changes, then record the session via the note-a-session-logging skill.
---

# OMG - Open Modular Gardening – Finalize & Commit Workflow

## When to use this skill

Use this skill when the user **explicitly** indicates that work should be finalized, for example by saying:

- "Finalize this"
- "Finalize the work"
- "Commit this"
- "Ship it"

Do not run this workflow implicitly.

## High-level steps

1. Generate a commit message from the conversation.
2. Run the test suite.
3. If tests are green, stage, commit, and push.
4. Create a session log in `doc/` using the `note-a-session-logging` conventions.

## Step 1 – Commit message from conversation

- Derive the commit message from:
  - The recent conversation.
  - The described intent and user stories.
- Prefer a short, descriptive subject line (max ~72 characters).
- Optionally add a brief body (1–3 bullet points) when helpful.
- Do **not** dump raw diffs into the commit message.

Example format:

```text
feat(garden): add basic garden CRUD

- Implement create/read/update/delete endpoints for gardens
- Enforce basic validation on surface area inputs
```

## Step 2 – Run tests

- Run the project test suite (for this .NET solution, default to `dotnet test` from the repository root or solution directory, as appropriate).
- If there are no tests, continue to the next step
- If tests **fail**:
  - Do **not** stage, commit, or push.
  - Summarize the failures back to the user.
  - Stop the finalize workflow.

## Step 3 – Commit

- If there are tests, and all tests are green:
  - Stage changes (for this case project, default to `git add .` unless the user has specified a narrower scope).
  - Commit with the generated message.
- Do not modify git configuration or use destructive commands.

## Step 4 – Document the session

- Then, create a new session log in `doc/` using the `note-a-session-logging` skill.
- The log should:
  - Reference the commit hash and branch.
  - Summarize the scope of work, mapped to user stories.
  - Capture key design decision.


## Step 5 - Commit and push
- `git add .`
- `git commit -m 'DOC: <name of the file>'` 
- Pull
  - If there are any conflicts that cannot be automatically merged, then pauze and let the user merge
- Push with a normal `git push` (no `--force`).


## Safety notes

- Only run this workflow when the user clearly asks to finalize or commit.
- Never force-push; use plain `git push`.
- If anything unexpected occurs (e.g., tests or push fail), report back to the user instead of guessing.

