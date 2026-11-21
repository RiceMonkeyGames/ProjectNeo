# TOOL-backlog-split | 2026-11-21 | Tooling & Docs - Per-Task Backlog Files, Archive Consolidation & Conflict/Token Optimization

- **Assignee**: SeongminHwang
- **Branch**: `feature/tool-backlog-split`
- **Scope**: Documentation & PowerShell agent tooling only (no Unity game code, scenes, or assets touched).

## Problem Solved

Parallel feature merges repeatedly conflicted on shared index files (`Docs/Backlog.md`, `Docs/archive/Backlog-Done.md`, `Docs/history/README.md`, `Docs/plan/Status.md`) and on forced `Last Updated:` header diffs across `Docs/`. At the same time, loading `Docs/Backlog.md` (12.5 KB) and `Docs/archive/Backlog-Done.md` (67.7 KB) inflated agent token consumption on every claim, completion, and conflict resolution.

## Changes Made

1. **1 Task = 1 File (`Docs/backlog/<TASK-ID>.md`)**:
   - Migrated all active backlog rows from `Docs/Backlog.md` into individual files under `Docs/backlog/<TASK-ID>.md` with a fixed 10-line YAML frontmatter header (`id`, `title`, `status`, `assignee`, `branch`, `area`, `depends_on`, `created`), preserving full task descriptions and rewriting relative doc links to `../`.
   - Replaced `Docs/Backlog.md` with a read-only routing stub so legacy links remain valid (`BROKEN_LINKS=0`) without future edits.
2. **Eliminated Shared Completion Bottlenecks**:
   - Froze `Docs/archive/Backlog-Done.md` as a read-only legacy archive (records through `COMBAT-10`); all subsequent completed tasks live exclusively in `Docs/history/<PREFIX>/<TASK-ID>.md` while removing `Docs/backlog/<TASK-ID>.md`.
   - Simplified `Docs/history/README.md` into a static prefix routing guide (no manual per-file link list or `Latest manual verification` line).
   - Scoped `Docs/plan/Status.md` to milestone gates rather than per-task appends.
   - Retired mandatory `Last Updated:` headers across `Docs/` (Git commit timestamps are the source of truth; `doc-finish-check.ps1` only checks timestamps when `-CheckTimestamps` is explicitly passed).
3. **Line Ending Policy**:
   - Added `*.md text eol=lf` and `*.ps1 text eol=crlf` to `.gitattributes` (without bulk `--renormalize`, avoiding unnecessary conflicts with open branches).
4. **Updated Agent Wrappers, Skills & Rules**:
   - `Tools/Agent/_common.ps1`: added UTF-8 console output handling, `Read-TaskFrontmatterFromLines`, `Read-TaskFrontmatter` (reads top 16 lines only), `Get-ActiveBacklogTasks`, and `Find-TaskHistoryPath`.
   - `Tools/Agent/backlog-list.ps1` (new): single-call filtered active/history task listing (`-Status`, `-Area`, `-Assignee`, `-IncludeHistory`).
   - Updated `Tools/Agent/session-brief.ps1`, `claim-check.ps1` (now also blocks reusing completed `Docs/history/` IDs), `doc-finish-check.ps1` (frontmatter validation, `ACTIVE_BACKLOG_EXISTS`, `UNARCHIVED_DONE_FILES`, `assignee:` + backtick-literal Korean exemption, opt-in `-CheckTimestamps`), and `sync-summary.ps1` (`Docs/backlog/` name-status + frontmatter summary).
   - Synchronized `AGENTS.md` (universal `backlog-list.ps1` rule for all agents), `Docs/INDEX.md`, `Docs/workflow/Collaboration.md`, `.claude/skills/{brief,claim,finish,sync-summary}/SKILL.md`, and `.claude/agents/docs-git.md`.

## Verification

- Verified scope: PowerShell 5.1 tooling checks, lossless migration parity (all 19 pre-existing active tasks + `TOOL-backlog-split` = 20 files), link/frontmatter/Korean/diff checks (`doc-finish-check.ps1`), and a clean two-branch concurrent claim/finish Git merge simulation (0 conflicts).
- Not applicable / not run: Unity compile, EditMode/PlayMode tests, and standalone build (no `Assets/`, `Packages/`, or `ProjectSettings/` files modified).

## Teammate Transition Note

For any pre-existing `feature/*` branch that still modified old `Docs/Backlog.md` or `Docs/archive/Backlog-Done.md`: when merging updated `develop`, keep the `develop` stub/frozen versions of those two files and delete only your completed `Docs/backlog/<TASK-ID>.md`.
