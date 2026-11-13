# LEGACY | 2026-11-12 | Workflow - Early Backlog & Documentation Rules

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: Shared Backlog Operationalization

- Added Development Backlog table (Task ID, Description, Status, Assignee, Completion Criteria) and operating procedures to the top of Google Docs. Preserved existing design text/images below 3 initial empty rows. Verified placement via browser inspection and Markdown export.
- Recorded shared link, claiming workflow, assignee collision protection, post-completion doc sync, and new task logging rules in AGENTS. Reconciled ImplementationPlan Sections 2/7 and Context collaboration guidelines.
- Doc-only modification; no game code, scenes, or assets altered. No Unity compilation, testing, or building run.
- Manual Verification: Open shared doc, press Ctrl+Home in Tab 1. "Development Backlog" header and 5-column table display above existing text. Reversible test: enter temporary task in blank row, verify status/assignee, clear input.
- Follow-up: Populated table with 15 initial tasks based on current plans (playtest verification, C-1a boundary consensus, morale/weapons/enemies decisions, waves, victory tally, casualties, rewards, scouting, facility decomposition, M1 integrated test). All unassigned; distinguished design gaps vs prerequisites.
- Added 5 trigger conditions for updating backlog below table; reflected in AGENTS/ImplementationPlan. Verified 3 pages of table/notes in browser and confirmed exported Markdown body matches pre-edit state.

### 2026-11-12: Timestamp Format Rule Standardized

- Standardized document last updated header to `Last Updated: YYYY-MM-DD HH:MM:SS (KST)`. Enforced updating to exact current time prior to saving across docs and AGENTS.

### 2026-11-12: Markdown Documentation Translated to English for Token Efficiency

- Translated repository Markdown documents (`AGENTS.md`, `Docs/Context.md`, `Docs/GameDesign.md`, `Docs/ImplementationPlan.md`, `Docs/History.md`) from Korean to English to reduce prompt token consumption.
- Maintained core rules: responses to the user remain in Korean, and timestamp formats are strictly preserved.
- Verified all file links, tables, technical identifiers, and architectural principles remain intact.

### 2026-11-12: Workflow Updated to Repository Markdown Backlog with Immediate Sync

- Migrated the backlog single source of truth from Google Docs to repository Markdown (`Docs/Backlog.md`).
- Updated workflow rules in `AGENTS.md`, `Docs/Backlog.md`, `Docs/Context.md`, `Docs/ImplementationPlan.md`, and `Docs/GameDesign.md`.
- Enforced new rule: When an agent or contributor claims an unclaimed task, they must mark it as `In Progress` with their name in `Docs/Backlog.md` and immediately commit/merge (push/sync) this change before implementing to prevent duplicate assignments and collisions.
