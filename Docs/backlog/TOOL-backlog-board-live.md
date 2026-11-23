---
id: TOOL-backlog-board-live
title: Live Local Server Refresh and Develop-Ref Data Source for Backlog Board
status: In Progress
assignee: SeongminHwang
branch: feature/tool-backlog-board-live
area: TOOL
depends_on: [TOOL-backlog-board-ui, BUG-backlog-board-encoding]
created: 2026-11-23
---

# TOOL-backlog-board-live — Live Local Server Refresh and Develop-Ref Data Source for Backlog Board

## Scope & Completion Criteria

Upgrade `Tools/Agent/backlog-board.ps1` and `Tools/Agent/backlog-board.bat` so the board reads `Docs/backlog/`, `Docs/history/`, and `Docs/archive/Backlog-Done.md` directly from `origin/develop` (with automatic `git fetch origin develop` and offline fallback to local `develop`) without touching the current working tree, and serves a lightweight `.NET HttpListener` localhost server (`http://127.0.0.1:41735/`) enabling in-page Refresh button and browser `F5` live updates (while preserving `-Static` / `-OutPath` / `-NoOpen` static export compatibility and pure 7-bit ASCII encoding).
