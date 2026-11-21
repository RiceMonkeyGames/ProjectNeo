---
id: TOOL-backlog-board
title: Local HTML Backlog Dashboard Script
status: In Progress
assignee: SeongminHwang
branch: feature/tool-backlog-board
area: TOOL
depends_on: []
created: 2026-11-22
---

# TOOL-backlog-board — Local HTML Backlog Dashboard Script

## Scope & Completion Criteria

Add a read-only PowerShell 5.1 script (`Tools/Agent/backlog-board.ps1`) that parses `Docs/backlog/*.md` (and optional `Docs/history/`) into a self-contained local HTML dashboard written outside the Git working tree (`%TEMP%\ProjectNeo\backlog-board.html` by default), with Kanban/table views, filters, search, `depends_on` blocked detection, body preview modal, and VS Code file/claim copy helpers without changing existing agent workflow scripts or backlog schema.
