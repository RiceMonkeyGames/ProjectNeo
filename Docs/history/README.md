# Past Verification History Index

> Purpose: Static folder routing rule for per-task completion logs. Do **not** edit this file on routine task completions (`TOOL-backlog-split`). | Parent: [INDEX](../INDEX.md) | Related: [Backlog](../Backlog.md), [Legacy Done Archive](../archive/Backlog-Done.md), [Status](../plan/Status.md)

Read this folder only when researching past design rationales, benchmarks, or verification evidence. Past test counts and temporary numbers do not represent current baselines.

- **Single source of truth for completed tasks**: Every completed task creates exactly one file at `Docs/history/<PREFIX>/<TASK-ID>.md` using header `# <TASK-ID> | <YYYY-MM-DD> | <Area> - <Title>`, and removes `Docs/backlog/<TASK-ID>.md`.
- **Listing completed tasks**: Do not manually maintain file lists here. Query the directory with:
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/backlog-list.ps1 -IncludeHistory`
  (optionally add `-Area <PREFIX>`).
- **Latest history by commit time**: `git log -n 10 --stat -- Docs/history/`

## Folder Mapping by Task Prefix

- `A/` — Legacy Stage A records (`Stage-A.md`)
- `B/`, `C/`, `D/`, `G/` — Legacy M0–M2 records (`<TASK-ID>.md`)
- `COMBAT/` — Direct combat, skills, consumables, hit/cover rules (`COMBAT-NN.md`)
- `SIEGE/` — Walls, placement, siege weapons, waves, enemy siege AI, morale/rout (`SIEGE-NN.md`)
- `FLOW/` — Scene transitions, run loop, outcome/reward/save flow (`FLOW-NN.md`)
- `PREP/` — Courtyard facilities, shop, squad org, equipment, crafting, build economy (`PREP-NN.md`)
- `META/` — Persistent saves, unlocks, perks, run score (`META-NN.md`)
- `CONTENT/` — Stage/hero/enemy/item rosters, bosses, balance (`CONTENT-NN.md`)
- `ART/`, `UI/`, `AUDIO/` — Art swaps, animations, HUD/UX, audio (`ART-NN.md`, `UI-NN.md`, `AUDIO-NN.md`)
- `Q/` — Design and architecture decisions (`Q-NN.md`)
- `BUG/`, `PERF/`, `QA/`, `DOC/`, `TOOL/` — Cross-cutting fixes, benchmarks, QA checks, docs, tooling (`<TYPE>-<slug>.md`)
- `LEGACY/` — Un-ID'd early records (`M0-*`, `M1-*`, `DOC-early-workflow.md`)
