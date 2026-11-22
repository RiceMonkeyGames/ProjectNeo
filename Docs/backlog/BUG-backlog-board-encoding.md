---
id: BUG-backlog-board-encoding
title: Fix PowerShell 5.1 CP949 Mis-Decoding of Non-ASCII Glyphs in backlog-board.ps1
status: In Progress
assignee: SeongminHwang
branch: fix/bug-backlog-board-encoding
area: BUG
depends_on: [TOOL-backlog-board-ui]
created: 2026-11-22
---

# BUG-backlog-board-encoding — Fix PowerShell 5.1 CP949 Mis-Decoding of Non-ASCII Glyphs in backlog-board.ps1

## Scope & Completion Criteria

Replace all raw non-ASCII Unicode glyphs in `Tools/Agent/backlog-board.ps1` with pure 7-bit ASCII (inline crisp SVG icons, HTML entities, and JS `\uXXXX` escapes) so Windows PowerShell 5.1 never corrupts HTML closing tags or JS string literals on CP949 / non-BOM checkouts.
