# TOOL-subagent-tuning | 2026-11-21 | Tool - Subagent Delegation Refinements

Last Updated: 2026-11-21 01:53:12 (KST)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md) | Related: [Collaboration](../../workflow/Collaboration.md), [AGENTS](../../../AGENTS.md)

### 2026-11-21: TOOL-subagent-tuning - Subagent Delegation Refinements

#### Background

- First session under [TOOL-subagent-default](TOOL-subagent-default.md) showed these problems:
  - `docs-git` stopped at its 20-turn limit midway through the three-branch `develop` merge. The main session finished the merge and fixed the archive row order.
  - The auto-mode permission check denied a `docs-git` claim commit and push to `develop`. The main session ran it after the user explicitly asked for it (claim commit `9813a70`).
  - A subagent's branch checkout changed files under the main session.
  - Commit trailers mixed Opus and Haiku model names.
  - A `sed` edit stripped CRLF line endings. The line endings were restored.

#### Decisions

- User decision, 2026-11-21: add the delegation refinements.
- User decision, 2026-11-21: the commit trailer always names the main-session model.

#### Changes

- `AGENTS.md`: added the sentence on exceptions kept in the main session to the "Default delegation (Claude Code)" bullet; header `Last Updated` refreshed.
- `Docs/workflow/Collaboration.md`: added item 11 "Delegation Refinements" under section 1; header `Last Updated` refreshed.
- `Docs/history/README.md`: registered this history file under `TOOL/`; header `Last Updated` refreshed.

#### Verification

- Doc-only change. No Unity run, no game code, no `Assets/` or `Packages/` changes.
- `Tools/Agent/doc-finish-check.ps1 -TaskId "TOOL-subagent-tuning"` (run at 2026-11-21 01:52:18 KST): `HISTORY_EXISTS=True`, `BROKEN_LINKS=0`, `STALE_LAST_UPDATED=0`, `KOREAN_IN_DOCS=0`, `CODEMAP_UNLISTED_SCRIPTS=0`, `UNARCHIVED_DONE_ROWS=0`, `GIT_DIFF_CHECK_OK=True`.

#### Outcome

- Done 2026-11-21 on user confirmation after review of the rule text; archived in [Backlog-Done](../../archive/Backlog-Done.md).
