# ProjectNeo Documentation Index

Last Updated: 2026-11-20 21:20:25 (KST)

> Purpose: Route agents to only the leaf docs needed for a task. | Parent: [AGENTS.md](../AGENTS.md) | Related: [Backlog](Backlog.md), [Collaboration](workflow/Collaboration.md)

**Reading Rule:** Open this file and [Backlog.md](Backlog.md) first, then read only the leaf files matching your task. Do not bulk-read `Docs/` or `history/`.

Shared workflow skills: Claude Code uses `/brief`, `/claim`, `/verify`, `/finish`, `/sync-summary` (and `.claude/agents/` subagents `docs-git`, `qa-verifier`, `repo-scout`); Codex uses `$brief`, `$claim`, `$verify`, `$finish`, `$sync-summary` from `.agents/skills/`, delegating to the same `.claude/skills/` instructions and `Tools/Agent/` wrappers. Details: [Collaboration](workflow/Collaboration.md).

## Task Routing

| Task Type | Read These Files |
|---|---|
| Pick a task / claim / branch / Task ID rules | [Backlog.md](Backlog.md) → [workflow/Collaboration.md](workflow/Collaboration.md) (or `/claim`, `/finish`, `/sync-summary`) |
| Check current status / active M1–M2 state | [plan/Status.md](plan/Status.md) |
| Check team ownership / shared file locks | [plan/Team.md](plan/Team.md) → [workflow/Collaboration.md](workflow/Collaboration.md) |
| Compile / EditMode & PlayMode tests / Windows build | [workflow/UnityCLI.md](workflow/UnityCLI.md) (or `/verify` / `Tools/Agent/unity-verify.ps1`) |
| Coding rules / art-swap decoupling rules | [workflow/Principles.md](workflow/Principles.md) |
| Find scripts, assets, or tests for a system | [codemap/README.md](codemap/README.md) → target module leaf |
| Manual verification controls & scenes | [codemap/Scenes.md](codemap/Scenes.md) (or [codemap/PrototypeScenes.md](codemap/PrototypeScenes.md)) |
| Check pending or past design decisions | [plan/Decisions-Pending.md](plan/Decisions-Pending.md) / [plan/Decisions-Resolved.md](plan/Decisions-Resolved.md) |
| Check game rules (combat / siege / morale / prep / meta) | [design/README.md](design/README.md) → target design leaf |
| Check past completion logs or benchmarks | [history/README.md](history/README.md) → `history/<PREFIX>/<TASK-ID>.md` |

## Directory Tree

- [Backlog.md](Backlog.md) — Active tasks only (`Done` tasks in [archive/Backlog-Done.md](archive/Backlog-Done.md)).
- `workflow/` — [Collaboration.md](workflow/Collaboration.md), [UnityCLI.md](workflow/UnityCLI.md), [Principles.md](workflow/Principles.md).
- `codemap/` — [README.md](codemap/README.md), [Scenes.md](codemap/Scenes.md), [PrototypeScenes.md](codemap/PrototypeScenes.md), [CombatAndSquads.md](codemap/CombatAndSquads.md), [WallsAndSiege.md](codemap/WallsAndSiege.md), [FlowRunEconomy.md](codemap/FlowRunEconomy.md), [EditorAndTests.md](codemap/EditorAndTests.md).
- `design/` — [README.md](design/README.md), [Combat.md](design/Combat.md), [WallsAndSiege.md](design/WallsAndSiege.md), [MoraleAndWave.md](design/MoraleAndWave.md), [PrepEconomyMeta.md](design/PrepEconomyMeta.md), [OpenQuestions.md](design/OpenQuestions.md).
- `plan/` — [Status.md](plan/Status.md), [Team.md](plan/Team.md), [Decisions-Pending.md](plan/Decisions-Pending.md), [Decisions-Resolved.md](plan/Decisions-Resolved.md), [Goal.md](plan/Goal.md), [Milestones.md](plan/Milestones.md), [M1.md](plan/M1.md).
- `history/` — [README.md](history/README.md) + per-task files under `<PREFIX>/<TASK-ID>.md` and `LEGACY/`.
- `archive/` — [Backlog-Done.md](archive/Backlog-Done.md), [GameDesignGoogleDoc.md](archive/GameDesignGoogleDoc.md).
