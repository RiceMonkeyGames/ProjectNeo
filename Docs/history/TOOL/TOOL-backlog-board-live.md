# TOOL-backlog-board-live | 2026-11-23 | Tooling - Live Local Server Refresh and Develop-Ref Data Source for Backlog Board

- **Assignee**: SeongminHwang
- **Branch**: `feature/tool-backlog-board-live`
- **Scope**: `Tools/Agent/backlog-board.ps1` live server / Git object reader upgrade and workflow documentation update (no Unity game code, scenes, or assets touched).

## Changes Made

1. **Always-`develop` Git Object Reader (`Tools/Agent/backlog-board.ps1`)**:
   - Runs `git fetch --quiet origin develop` (unless `-NoFetch` is passed) and reads `Docs/backlog/`, `Docs/history/`, and `Docs/archive/Backlog-Done.md` directly from `origin/develop` (with automatic offline fallback to local `develop`) via `git ls-tree` and chunked `git cat-file --batch`.
   - Leaves the user's active working tree, checked-out branch, and uncommitted edits completely untouched.
   - Displays `origin/develop @ <sha>` in the sidebar header and notes the local working-tree branch underneath when different from `develop`.
2. **In-Page Live Refresh via Built-In `.NET HttpListener` (`http://127.0.0.1:41735/`)**:
   - Running `Tools/Agent/backlog-board.bat` (or `backlog-board.ps1 -Serve`) starts a single detached hidden loopback server (reusing an existing instance via `/api/ping` if already running, with 60-minute idle auto-shutdown) and opens `http://127.0.0.1:41735/`.
   - Added topbar `Refresh` button (keyboard shortcut `r`) calling `/api/refresh` to fetch `origin/develop` and update the dashboard in-place while preserving active filters, view tab, and open modal. Browser `F5` also fetches fresh `origin/develop` data.
   - Added sidebar live status indicator and one-click `Stop` server button (`/api/shutdown`).
   - Preserved static snapshot compatibility (`-Static`, `-OutPath`, or `-NoOpen`) and pure 7-bit ASCII encoding (`NON_ASCII_BYTES=0`).
3. **Documentation**:
   - Updated the `backlog-board.ps1` description in [Collaboration.md](../../workflow/Collaboration.md).

## Verification

- Verified scope: `NON_ASCII_BYTES=0` check; static mode (`backlog-board.bat -NoOpen`, reading `origin/develop@7f1acde` while on `feature/tool-backlog-board-live`); live server startup, `/api/ping`, `/api/refresh`, headless Microsoft Edge 1440x900 live HTTP render and visual screenshot inspection, `/api/shutdown` cleanup verification; and `doc-finish-check.ps1 -TaskId "TOOL-backlog-board-live" -StrictDocs`.
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).
