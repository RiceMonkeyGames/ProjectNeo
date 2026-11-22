# TOOL-backlog-board-ui | 2026-11-22 | Tooling - Modern Dashboard UI Upgrade for Local HTML Backlog Board

- **Assignee**: SeongminHwang
- **Branch**: `feature/tool-backlog-board-ui`
- **Scope**: HTML/CSS/JS template upgrade in `Tools/Agent/backlog-board.ps1` (no Unity game code, scenes, assets, or backlog schema changes).

## Changes Made

1. **Modern Linear/Notion-Inspired Layout (`Tools/Agent/backlog-board.ps1`)**:
   - Added a sticky left sidebar with view navigation (`Kanban Board`, `Table View`, `Completed History`), quick status filters, **Assignees** workload list (initials avatar, active `In Progress` count + total count), and **Areas** filter list with domain color dots and counts.
   - Added a sticky top bar with active filter chips (one-click clear), `/` shortcut search, `Ready to Claim` toggle, and view switcher (`1`/`2`/`3` keyboard shortcuts).
   - Added a compact KPI strip with a segmented status progress bar.
   - Upgraded Kanban cards with left status color accent bars, inline dependency pills (`active`/`done`/`unknown`), assignee avatars, and branch tags.
   - Upgraded the detail modal with Notion-style property rows, clickable dependency jump links for active tasks, and refined dark/light themes.

## Verification

- Verified scope: PowerShell 5.1 generation via `backlog-board.ps1 -NoOpen`, `-NoHistory -OutPath`, and `backlog-board.bat -NoOpen`; JSON payload and UTF-8 Korean assignee validation; `doc-finish-check.ps1 -TaskId "TOOL-backlog-board-ui" -StrictDocs`.
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).
