# TOOL-subagent-default | 2026-11-21 | Tool - Default Subagent Delegation Rules

Last Updated: 2026-11-21 01:38:56 (KST)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md) | Related: [Collaboration](../../workflow/Collaboration.md), [AGENTS](../../../AGENTS.md)

### 2026-11-21: TOOL-subagent-default - Default Subagent Delegation Rules

#### Background

- `AGENTS.md` described the three Claude Code role subagents (`docs-git`, `qa-verifier`, `repo-scout`) but did not say when to use them.
- Claude Code spawns subagents only when asked by default. Sessions therefore did the work inline, for example the PREP-09 commit and push.

#### Decisions

- User decision, 2026-11-21: Claude Code uses the role subagents by default without being asked each time.
  - `docs-git`: Backlog claim/archive, history/codemap/`Last Updated` doc updates, and user-approved commits/pushes.
  - `qa-verifier`: post-implementation verification (code rules, Unity tests, diff audit against design, manual-check draft).
  - `repo-scout`: lookups that span many files or docs.
- Delegation does not widen Git authorization. Main session keeps code edits, design questions, approval gates, and one-command checks.
- Tracked as a new TOOL task on its own branch (`feature/tool-subagent-default`).
- Codex is unaffected: it has no subagents and keeps working inline.

#### Changes

- `AGENTS.md`: added the "Default delegation (Claude Code)" bullet under "Shared Workflow Skills & Subagents"; header `Last Updated` refreshed.
- `Docs/workflow/Collaboration.md`: added item 10 "Default Delegation" under section 1; header `Last Updated` refreshed.
- `Docs/history/README.md`: registered this history file under `TOOL/`.
- The claim commit (`4317f1e`, `docs: claim TOOL-subagent-default`) was the first use of the new rule: it was made through `docs-git`.

#### Verification

- Doc-only change. No Unity run, no game code, no `Assets/` or `Packages/` changes.
- `Tools/Agent/doc-finish-check.ps1 -TaskId "TOOL-subagent-default"`: `UNARCHIVED_DONE_ROWS=0`, `HISTORY_EXISTS=True`, `BROKEN_LINKS=0`, `STALE_LAST_UPDATED=0`, `KOREAN_IN_DOCS=0`, `CODEMAP_UNLISTED_SCRIPTS=0`, `GIT_DIFF_CHECK_OK=True` (final run at 2026-11-21 01:37:03 KST).
- Manual verification: none required beyond review of the rule text in `AGENTS.md` and `Docs/workflow/Collaboration.md`.

#### Outcome

- Done 2026-11-21 on user confirmation; archived in [Backlog-Done](../../archive/Backlog-Done.md).
