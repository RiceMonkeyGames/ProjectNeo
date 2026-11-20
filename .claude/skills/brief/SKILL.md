---
name: brief
description: Run a single-call session bootstrap digest (KST time, branch, Assets/ThirdParty submodule status, dirty count, ahead/behind origin/develop, unity status, and compact In Progress Backlog locks). Use at session start or after /clear.
disable-model-invocation: false
---

# /brief — Session Start Digest

Replace multi-command session warmup with one bounded script call.

## Arguments
- `$ARGUMENTS`: Optional flags passed to the script:
  - `-Fetch`: run `git fetch origin` before computing ahead/behind counts.
  - `-SkipUnity`: skip `unity status` check (useful for doc-only tasks).

## Steps

1. Run the session brief wrapper (single command):
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/session-brief.ps1
   ```
   (Append `-Fetch` or `-SkipUnity` if requested.)

2. Act on status lines per `AGENTS.md`:
   - If `SUBMODULE_STATUS` is `NEEDS_INIT` or `DIVERGED`: run `git submodule update --init --recursive` without asking (and inform the user to refresh Unity if open). If auth fails, stop and report read-access requirement for `ProjectNeo-Assets`.
   - Use `ACTIVE_IN_PROGRESS` to avoid overlapping another assignee's active task or shared files.
   - Use `KST_NOW` if editing docs in the same turn.

3. Report the current branch, submodule/Unity readiness, and relevant active locks concisely in Korean.
