# TOOL-backlog-board | 2026-11-22 | Tooling - Read-Only Local HTML Backlog Dashboard

- **Assignee**: SeongminHwang
- **Branch**: `feature/tool-backlog-board`
- **Scope**: Read-only PowerShell 5.1 dashboard generator (`Tools/Agent/backlog-board.ps1`) and workflow documentation update (no Unity game code, scenes, or assets touched).

## Changes Made

1. **Added `Tools/Agent/backlog-board.ps1`**:
   - Reuses `Get-ActiveBacklogTasks` from `Tools/Agent/_common.ps1` to read `Docs/backlog/*.md` frontmatter without altering any existing workflow script or backlog file schema.
   - Resolves `depends_on` entries against active `Docs/backlog/`, `Docs/history/`, and frozen `Docs/archive/Backlog-Done.md` to mark active blockers (`BLOCKED`).
   - Generates a self-contained single-file HTML dashboard (dark/light theme, KPI summary cards, Kanban/Table/History tabs, search, Area/Assignee/Status/Ready-to-Claim filters, task body preview modal, `vscode://file/` open button, and `/claim` / `claim-check` / path copy buttons).
   - Writes output outside the repository by default (`%TEMP%\ProjectNeo\backlog-board.html`, configurable via `-OutPath`, with `-NoOpen` and `-NoHistory` switches) so Git status, merges, Unity `Temp/`, and AI agent token usage remain unaffected.
2. **Documentation**:
   - Listed `backlog-board.ps1` alongside the shared `Tools/Agent/` wrappers in [Collaboration.md](../../workflow/Collaboration.md).

## Verification

- Verified scope: PowerShell 5.1 execution (`backlog-board.ps1 -NoOpen` and `-NoHistory -OutPath`), JSON payload validation (Korean assignee UTF-8 preservation, `depends_on` active/done resolution on `UI-07` and `UI-08`), existing `backlog-list.ps1` parity, and `doc-finish-check.ps1 -TaskId "TOOL-backlog-board" -StrictDocs`.
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).
