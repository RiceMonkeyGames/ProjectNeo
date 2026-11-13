---
name: finish
description: Run post-implementation documentation, link, and diff checks, present manual verification steps, and after explicit user confirmation mark the Backlog row Done, archive it, and commit/push. Use when wrapping up a task or running /finish.
disable-model-invocation: false
---

# /finish — Task Completion, Doc Checks & Handoff

Enforces `AGENTS.md` completion rules and asks for user confirmation before marking `Done` / archiving / committing.

## Arguments
- `$ARGUMENTS`: Optional `<TASK-ID>`. If omitted, pass without `-TaskId` or infer from current branch (`feature/<id>`).

## Phase 1: Technical & Documentation Pre-Check (Before Marking Done)

1. Run the completion check script:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/doc-finish-check.ps1 -TaskId "<TASK-ID>"
   ```
2. Review the output:
   - `KST_NOW`: use this exact timestamp for `Last Updated: YYYY-MM-DD HH:MM:SS (KST)` in edited docs.
   - `HISTORY_EXISTS`: if `False`, create `Docs/history/<PREFIX>/<TASK-ID>.md` and register it in `Docs/history/README.md` (refer to `Docs/history/README.md` for folder mapping: `TOOL/`, `BUG/`, `COMBAT/`, etc.).
   - `BROKEN_LINKS`: if `> 0`, fix all broken markdown links shown.
   - `GIT_DIFF_CHECK_OK`: if `False`, fix whitespace/conflict markers.
   - Update any affected leaf docs (`Docs/INDEX.md`, `Docs/codemap/*`, `Docs/design/*`, `Docs/workflow/*`, `Docs/plan/*`). Do not bulk-read unrelated `Docs/`.

3. Present to the user:
   - Exact scope verified (automated checks vs not-yet-run manual checks)
   - Concrete **manual verification guide** (exact scene/file paths, commands or controls, expected results, reset steps)
   - Explicit question asking whether to mark `<TASK-ID>` as `Done`, move its row to `Docs/archive/Backlog-Done.md`, and commit/push to the work branch.

## Phase 2: Archive & Push (Only After User Confirmation)

4. Once the user confirms:
   - Move the `<TASK-ID>` row out of `Docs/Backlog.md` into `Docs/archive/Backlog-Done.md` with status `Done` and update `Last Updated: <KST_NOW> (KST)` on both files.
   - Re-run `doc-finish-check.ps1 -TaskId "<TASK-ID>"` and verify `UNARCHIVED_DONE_ROWS=0`, `HISTORY_EXISTS=True`, `BROKEN_LINKS=0`, `GIT_DIFF_CHECK_OK=True`.
   - Stage, commit, and push the work branch (`git push -u origin <current-branch>`), leaving `develop` merge and remote branch deletion to the user.
