---
name: claim
description: Claim a Waiting Backlog task or register a new task ID on synced develop, commit and push the claim, and switch to feature/<task-id-lowercase> or fix/<task-id-lowercase>. Use when starting a task or running /claim.
disable-model-invocation: false
---

# /claim — Task Claim & Branch Setup

Follow `AGENTS.md` and `Docs/workflow/Collaboration.md` rules with minimal shell turns.

## Arguments
- `$ARGUMENTS`: Optional `<TASK-ID>` (e.g. `COMBAT-01`, `BUG-camera-clip`, `TOOL-agent-workflows`). If omitted, ask the user which `Waiting` item from `Docs/Backlog.md` to claim or what new ID to create.

## Steps

1. Run the preflight check (single command):
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/claim-check.ps1 -TaskId "<TASK-ID>"
   ```
   - Read `KST_NOW`, `CURRENT_BRANCH`, `DIRTY_FILES`, `IN_BACKLOG`, `TASK_STATUS`, `TASK_ASSIGNEE`, `CLAIM_OK`, and `SUGGESTED_BRANCH`.
   - If exit code is `4`: stop and fix the invalid ID (`[A-Za-z0-9-]+` only; follow `<DOMAIN>-<NN>`, `BUG-<slug>`, `Q-NN`, etc. per `Docs/workflow/Collaboration.md`).
   - If exit code is `3`: another task/assignee already owns it (`In Progress` / `Done`). Stop and report the conflict to the user; do not overwrite.
   - If `DIRTY_FILES` > `0`: do not force `git checkout`; report dirty files and ask how to proceed (or use a worktree).

2. Ensure local `develop` matches `origin/develop` (`git checkout develop && git pull --ff-only origin develop`), then edit `Docs/Backlog.md` immediately:
   - If `IN_BACKLOG=True` (`Waiting`): set `Status` to `In Progress`, `Assignee` to the current developer/agent name, and record `Branch \`<SUGGESTED_BRANCH>\`` in `Completion Criteria & Prerequisites`.
   - If `IN_BACKLOG=False` (new task): add a new `In Progress` row in the active table of `Docs/Backlog.md` and note `Branch \`<SUGGESTED_BRANCH>\``.
   - Update `Last Updated: <KST_NOW> (KST)` at the top of `Docs/Backlog.md`.

3. Commit & push the claim to `develop`, then create the work branch immediately (unless blocked by user-only push preference or dirty state):
   ```powershell
   git add Docs/Backlog.md
   git commit -m "docs: claim <TASK-ID>"
   git push origin develop
   git checkout -b <SUGGESTED_BRANCH>
   ```

4. Report claimed ID, branch name, and next implementation scope concisely in Korean.
