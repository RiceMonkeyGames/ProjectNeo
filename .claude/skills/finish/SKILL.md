---
name: finish
description: Run post-implementation documentation, link, frontmatter, and diff checks, present manual verification steps, and after explicit user confirmation remove Docs/backlog/<TASK-ID>.md and commit/push. Use when wrapping up a task or running /finish.
disable-model-invocation: false
---

# /finish — Task Completion, Doc Checks & Handoff

Enforces `AGENTS.md` completion rules and asks for user confirmation before removing the active backlog file and committing/pushing.

## Arguments
- `$ARGUMENTS`: Optional `<TASK-ID>`. If omitted, pass without `-TaskId` or infer from current branch (`feature/<id>` / `fix/<id>`).

## Phase 1: Technical & Documentation Pre-Check (Before Marking Done)

1. Run the completion check script:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/doc-finish-check.ps1 -TaskId "<TASK-ID>"
   ```
2. Review the output:
   - `HISTORY_EXISTS`: if `False`, create `Docs/history/<PREFIX>/<TASK-ID>.md` using header `# <TASK-ID> | <YYYY-MM-DD> | <Area> - <Title>`. Do **not** edit `Docs/history/README.md`, `Docs/archive/Backlog-Done.md`, `Docs/Backlog.md`, or `Docs/plan/Status.md` for routine task completions.
   - `BROKEN_LINKS`: if `> 0`, fix all broken markdown links shown.
   - `INVALID_BACKLOG_FILES`: if `> 0`, fix frontmatter schema errors in listed `Docs/backlog/*.md` files.
   - `KOREAN_IN_DOCS`: if `> 0`, rewrite non-name prose in `Docs/` to English (verbatim Korean UI captions belong in inline backticks).
   - `CODEMAP_UNLISTED_SCRIPTS`: if `> 0`, register newly added runtime scripts in the matching `Docs/codemap/*.md` leaf.
   - `GIT_DIFF_CHECK_OK`: if `False`, fix whitespace/conflict markers.
   - Update any affected leaf docs (`Docs/codemap/*`, `Docs/design/*`, `Docs/workflow/*`, `Docs/plan/Decisions-*.md`). Do **not** add or edit `Last Updated:` headers, and do not bulk-read unrelated `Docs/`.

3. Present to the user:
   - Exact scope verified (automated checks vs not-yet-run manual checks)
   - Concrete **manual verification guide** (exact scene/file paths, commands or controls, expected results, reset steps)
   - Explicit question asking whether to mark `<TASK-ID>` Done (remove `Docs/backlog/<TASK-ID>.md`) and commit/push to the work branch.

## Phase 2: Complete & Push (Only After User Confirmation)

4. Once the user confirms:
   - Remove the active task file (`git rm "Docs/backlog/<TASK-ID>.md"`). `Docs/history/<PREFIX>/<TASK-ID>.md` is the single source of truth for completed tasks.
   - Re-run `doc-finish-check.ps1 -TaskId "<TASK-ID>"` and verify `ACTIVE_BACKLOG_EXISTS=False`, `UNARCHIVED_DONE_FILES=0`, `INVALID_BACKLOG_FILES=0`, `HISTORY_EXISTS=True`, `BROKEN_LINKS=0`, `GIT_DIFF_CHECK_OK=True`.
   - Stage, commit, and push the work branch (`git push -u origin <current-branch>`), leaving `develop` merge and remote branch deletion to the user.
