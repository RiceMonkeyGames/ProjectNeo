# TOOL-backlog-board-bat | 2026-11-22 | Tooling - Batch Launcher for Local HTML Backlog Board

- **Assignee**: SeongminHwang
- **Branch**: `feature/tool-backlog-board-bat`
- **Scope**: Windows `.bat` launcher (`Tools/Agent/backlog-board.bat`) and workflow documentation update (no Unity game code, scenes, or assets touched).

## Changes Made

1. **Added `Tools/Agent/backlog-board.bat`**:
   - Invokes `"%~dp0backlog-board.ps1"` with `-NoProfile -ExecutionPolicy Bypass` so it can be double-clicked from Explorer, pinned as a desktop shortcut, or executed from any working directory.
   - Forwards optional CLI arguments (`%*`, such as `-NoOpen`, `-NoHistory`, `-OutPath`).
   - Exits cleanly on success and pauses only on non-zero exit codes so errors remain readable.
2. **Documentation**:
   - Noted `backlog-board.bat` alongside `backlog-board.ps1` in [Collaboration.md](../../workflow/Collaboration.md).

## Verification

- Verified scope: Executed `cmd /c "Tools\Agent\backlog-board.bat -NoOpen"` from both repository root and an external working directory (`%TEMP%`), and ran `doc-finish-check.ps1 -TaskId "TOOL-backlog-board-bat" -StrictDocs`.
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).
