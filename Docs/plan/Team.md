# Team Division of Labor & Roadmap Governance

Last Updated: 2026-11-16 17:37:56 (KST)

> Purpose: Assignee ownership matrix, shared modification rights, and roadmap governance rules. | Parent: [INDEX](../INDEX.md) | Related: [Collaboration](../workflow/Collaboration.md), [Backlog](../Backlog.md)

## Assignment Matrix & Shared Modification Rights

| Assignee | Owned Scope | Locked Files (Do Not Touch) |
|---|---|---|
| Teammate | Enemy assault, siege behavior execution (`B-6`, `B-7`, `B-8`, `B-9`). | Scene flow framework, game entry scenes (`Title`, `Game`). |
| User | Game entry and loop architecture (`G-0`, `G-1`): Scene switches, Title → Prep → Combat → Reward/Results phases, run data lifecycle (`C-1a`, `C-1~2`). | Shared combat core (`CharacterActor`, `UnitCombatAI`), isolated enemy test scenes. |

Teammate scope (`B-6~B-9`) is our working split; confirm exact boundaries before touching shared contracts. Design questions go to the user first.

- **Prototype vs real scenes (2026-11-11)**: `*Prototype` scenes stay as standalone demos; real game loop lives in `Title` and `Game`.
- **G-0 / G-1 state**: Wired in `Title` and `Game`; both scenes appended to build list (first build scene is `Title` since QA-standalone-build, 2026-11-16; prototype scenes follow). Default castle (2026-11-12): 80×100 grid (240×300m), 3m cells, up to 3 tiers, 96×96m castle, 90×90m courtyard, 120m northern field, 5 placeholder facilities, player start at south courtyard `(40, 34)`. Temporary G-1 defaults: 3 × 4 squads, reused `ConstructionHud`, no stage reset except survivor healing.
- **Battle–flow interface (2026-11-12)**: `GameBattleController` starts on entering `GamePhase.Battle` via `GameSession.Flow.PhaseChanged` and calls `GameSession.Flow.FinishBattle` once on end. Change build list first scene only after notifying teammate (changed to `Title` on user decision 2026-11-16). Only one person edits `Game.unity` at a time.

## Parallel Workflow Matrix (3-Developer Reference)

| Batch | Developer 1 (Walls) | Developer 2 (Combat/AI) | Developer 3 (Flow/Run) | Exit Criteria |
|---|---|---|---|---|
| W0 Contracts | Grid/gate specs | Command & AI interfaces | Run lifecycle & outcome contracts | Agree on B-0 contracts & boundaries |
| W1 Core Rules | B-4.1~2 Grid/placement | B-5.1 Squad commands | C-1a Run data contracts & tests | Pure C# unit tests pass |
| W2 Scene Wiring | B-4.3~5 Wall physics/NavMesh | B-5.2 AI & B-6.1 archetypes | B-8.1 Morale & B-9.1 Outcome | Isolated scene tests pass |
| W3 Integration | B-4.6~7 Siege weapons | B-5.3~4 Wall-top commands | B-7.1 Spawns & B-8.2 Rout | Wall-top command integration verified |
| W4 Full Combat | B-6.3 Ladder/aerial support | B-6.2~4 Enemy assault | B-7.2 Cleanup & B-9.2 Triggers | All roles active in siege scene |
| W5 M1 Closeout | Pathfinding stress test | Command/AI regression | Outcome & casualty carry-over | M1 sign-off; detail M2 |

## Concurrency & Roadmap Governance

- Pure C# models and isolated tests run in parallel; `CharacterActor`/`UnitCombatAI`, shared prefabs, integration scenes, project settings, and packages update sequentially. Never run compile/PlayMode/tests/builds concurrently in one editor.
- Check milestones, pending decisions, and [Backlog.md](../Backlog.md) before claiming tasks (`In Progress` + immediate push).
- Record user decisions immediately in `Docs/design/` and `Docs/plan/`.
- Update task status, `Docs/codemap/`, and `Docs/history/<PREFIX>/<TASK-ID>.md` on completion; delete remote task branch after confirmed `develop` merge.
- Explicitly distinguish automated tests vs manual playtests, prototype scenes vs integrated scenes, and editor vs standalone builds.
