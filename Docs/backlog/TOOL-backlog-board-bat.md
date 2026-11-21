---
id: TOOL-backlog-board-bat
title: Batch Launcher for Local HTML Backlog Board
status: In Progress
assignee: SeongminHwang
branch: feature/tool-backlog-board-bat
area: TOOL
depends_on: [TOOL-backlog-board]
created: 2026-11-22
---

# TOOL-backlog-board-bat — Batch Launcher for Local HTML Backlog Board

## Scope & Completion Criteria

Add a double-clickable Windows `.bat` launcher (`Tools/Agent/backlog-board.bat`) that runs `Tools/Agent/backlog-board.ps1` from any working directory, forwards CLI arguments (`%*`), and pauses only on error.
