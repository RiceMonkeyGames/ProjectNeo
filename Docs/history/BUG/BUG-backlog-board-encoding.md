# BUG-backlog-board-encoding | 2026-11-22 | Bugfix - Pure ASCII Fix for PowerShell 5.1 CP949 Mis-Decoding in backlog-board.ps1

- **Assignee**: SeongminHwang
- **Branch**: `fix/bug-backlog-board-encoding`
- **Scope**: `Tools/Agent/backlog-board.ps1` encoding/rendering fix and minor CSS polish (no Unity game code, scenes, assets, or backlog schema changes).

## Root Cause

`Tools/Agent/backlog-board.ps1` was stored as UTF-8 without BOM while containing 3-byte UTF-8 Unicode glyphs inside the embedded HTML/JS template. On Korean Windows (CP949), PowerShell 5.1 decoded the file as ANSI, causing trailing UTF-8 bytes to merge with immediately following `<` or `'` characters — breaking HTML closing tags (`??/span>`, `??/button>`) and JS string literals (`SyntaxError`).

## Changes Made

1. **Pure 7-bit ASCII Template (`Tools/Agent/backlog-board.ps1`)**:
   - Replaced all 15 non-ASCII Unicode glyphs with inline ASCII SVG icons (branch, kanban, table, history, theme, menu, search), HTML entities (`&mdash;`, `&times;`, `&#8627;`, `&#9650;`, `&#9660;`), and JS Unicode escape (`\u2014`).
   - Verified `NON_ASCII_BYTES=0` so the script is immune to PowerShell 5.1 codepage mis-decoding regardless of BOM or system locale.
2. **Desktop Layout Polish**:
   - Fixed `.btn.menu-btn` specificity so the mobile hamburger button stays hidden on desktop widths.
   - Adjusted Kanban column min-width to `245px` so all 4 primary columns fit side-by-side at 1440px without wrapping.

## Verification

- Verified scope: `NON_ASCII_BYTES=0` check, `Tools\Agent\backlog-board.bat -NoOpen` execution, headless Microsoft Edge Chromium DOM render (`CARDS=20`, `COLS=4`, `INPROG=4`, zero broken tags) and 1440x900 visual screenshot inspection, and `doc-finish-check.ps1 -TaskId "BUG-backlog-board-encoding" -StrictDocs`.
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).
