---
name: claim
description: Claim a Waiting Docs/backlog/<TASK-ID>.md task or register a new task file on synced develop, commit and push the claim, and switch to feature/<task-id-lowercase> or fix/<task-id-lowercase>. Use when starting a task or running /claim.
disable-model-invocation: false
---

# /claim — Task Claim & Branch Setup

Follow `AGENTS.md` and `Docs/workflow/Collaboration.md` rules with minimal shell turns.

## Arguments
- `$ARGUMENTS`: Optional `<TASK-ID>` (e.g. `COMBAT-01`, `BUG-camera-clip`, `TOOL-agent-workflows`). If omitted, run `Tools/Agent/backlog-list.ps1 -Status Waiting` and ask the user which task to claim or what new ID to create.

## Steps

1. Run the preflight check (single command):
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/claim-check.ps1 -TaskId "<TASK-ID>"
   ```
   - Read `KST_NOW`, `CURRENT_BRANCH`, `DIRTY_FILES`, `TASK_FILE`, `IN_BACKLOG`, `IN_HISTORY`, `TASK_STATUS`, `TASK_ASSIGNEE`, `CLAIM_OK`, and `SUGGESTED_BRANCH`.
   - If exit code is `4`: stop and fix the invalid ID (`[A-Za-z0-9-]+` only; follow `<DOMAIN>-<NN>`, `BUG-<slug>`, `Q-NN`, etc. per `Docs/workflow/Collaboration.md`).
   - If exit code is `3`: another assignee already owns it (`In Progress`), it is marked `Done`, or it was already completed in `Docs/history/`. Stop and report the conflict to the user; do not overwrite.
   - If `DIRTY_FILES` > `0`: do not force `git checkout`; report dirty files and ask how to proceed (or use a worktree).

2. Ensure local `develop` matches `origin/develop` (`git checkout develop` then `git pull --ff-only origin develop`), then edit or create **only** `Docs/backlog/<TASK-ID>.md` (never edit `Docs/Backlog.md` stub):
   - If `IN_BACKLOG=True` (`Waiting`): set `status: In Progress`, `assignee:` to the human requester's name (never an AI tool name), and `branch: <SUGGESTED_BRANCH>` in the YAML frontmatter.
   - If `IN_BACKLOG=False` (new task): create `Docs/backlog/<TASK-ID>.md` following the fixed frontmatter schema in `Docs/workflow/Collaboration.md` (`id`, `title`, `status: In Progress`, `assignee: <human>`, `branch: <SUGGESTED_BRANCH>`, `area`, `depends_on`, `created: YYYY-MM-DD`).

3. Commit & push the claim to `develop` (main session handles `develop` push per `TOOL-subagent-tuning`), then create the work branch immediately:
   ```powershell
   git add "Docs/backlog/<TASK-ID>.md"
   git commit -m "docs: claim <TASK-ID>"
   git push origin develop
   git checkout -b <SUGGESTED_BRANCH>
   ```
   (If `origin/develop` advanced with another teammate's separate task file, `git pull --rebase origin develop` before pushing.)

4. Report claimed ID, branch name, and next implementation scope concisely in Korean.
