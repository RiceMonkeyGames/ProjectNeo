# DOC-doc-tree-and-id | 2026-11-13 | Docs/Workflow - Tree Sharding & Task ID Rules

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-13: DOC-doc-tree-and-id - Docs Tree Sharding & Task ID Convention

- Assignee: Hwang Seong-min (agent); claimed on `develop` (`394374b`) and implemented on `feature/doc-tree-and-id`.
- User decisions applied:
  1. M3+ feature Task IDs use domain prefixes (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`) + `-NN`; cross-cutting tasks keep `Q-NN`, `BUG-<slug>`, `PERF-<slug>`, `DOC-<slug>`, `TOOL-<slug>` plus `QA-<slug>`. Allowed characters: `A-Z`, `0-9`, `a-z`, `-` only.
  2. Normalized unstarted irregular IDs in `Docs/Backlog.md`: `B-9.3-play` → `QA-game-loop`, `B-9.2-wave` → `SIEGE-01`, `B-4.6-action` → `SIEGE-02`. Kept `In Progress` `C-1a-confirm`.
  3. Kept reserved M3 tracking IDs `E-1~4` in `Docs/plan/Milestones.md` until M3 kickoff; new tasks follow the domain/type convention.
  4. Sharded `Docs/Context.md`, `Docs/ImplementationPlan.md`, `Docs/GameDesign.md`, and `Docs/History.md` into `Docs/INDEX.md` + `workflow/`, `codemap/`, `design/`, `plan/`, `history/`, and `archive/` (`Backlog-Done.md`, `GameDesignGoogleDoc.md`). Updated `AGENTS.md` to route via `Docs/INDEX.md`.
- Doc-only change; no C# scripts, scenes, or Unity assets modified; no Unity compilation or tests required.
- Verification: Checked file sizes, broken internal markdown link scan, `git diff --check`, and confirmed all 48 `History.md` entries and active/done backlog rows were preserved.
- Manual verification: Open [AGENTS.md](../../../AGENTS.md) and [Docs/INDEX.md](../../INDEX.md) to verify routing links; inspect [Docs/Backlog.md](../../Backlog.md) for normalized active IDs (`QA-game-loop`, `SIEGE-01`, `SIEGE-02`) and [Docs/archive/Backlog-Done.md](../../archive/Backlog-Done.md) for archived tasks. Merge `feature/doc-tree-and-id` into `develop` when ready and remove remote branch after confirmation.
