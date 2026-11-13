# Current Implementation Status & Starting Point

Last Updated: 2026-11-13 13:25:55 (KST)

> Purpose: Active milestone state, implemented vs pending connections, and M2 early deliverables. | Parent: [INDEX](../INDEX.md) | Related: [Milestones](Milestones.md), [Backlog](../Backlog.md), [Scenes](../codemap/Scenes.md)

**Current Milestone: M1 Single Siege Battle in progress; M2 Repeatable Run Loop in progress in parallel.**

Merged `syj`'s Title/Game flow (G-0, G-1), wall stacking / overlapping unit elevation, and `feature/siege-prototype` test scene into `develop`. `Game` scene links 1st-person prep / construction F interaction → gate banner F interaction → enemy assault, ladders, actual win/loss → rewards/results. On the M2 side, run data (`C-1~2`), victory rewards (`C-4`), build/repair costs (`D-2`), facility pop-ups (`D-0`), upgrades (`D-1`), shop (`D-5`), player equipment (`D-7`), grunt spawning (`D-0b`), and barracks recruitment/training (`D-4`) are implemented; formal stage/wave layouts, casualty carry-over (`C-3`), tavern (`D-3`), squad org (`D-8`), and blacksmith (`D-6`) remain follow-ups. Wall-destroying enemies, aerial enemies, morale, and siege weapon firing (`Q-21`) remain pending.

## Implemented vs Pending Real Connections

| Domain | Verified Current State | Remaining Integration Scope |
|---|---|---|
| Project Foundation | Unity 6000.6.8f1, URP, 4 assemblies, test runners, Windows build script | Distribution packaging, release verification |
| Stats, Economy, Facilities | Stat modifiers, multi-material wallet, inventory, trade logic, facility pop-ups/upgrades, shop, barracks recruitment/training | Tavern/blacksmith/squad org UIs, crafting workflows |
| Characters & Combat | 1st-person movement, melee attacks, health, grunt stats, projectiles, auto-combat, Game scene siege assault/ladders/outcomes | Wall-destroying/aerial enemies, active skills, consumables, production HUD |
| Meta & Perks | Progression curves, reincarnation conversion, gacha unlocks, perk payloads | Disk persistence, run reward routing, runtime perk injection |
| Squads, Morale, Walls | Squad structures, grid occupancy/rotation, stacking, async NavMesh rebuild, fall damage, weapon mounting/collapse, 1st-person commands | Morale/rout mechanics, siege weapon firing, wall-destroying enemies, construction delays |
| Run Data Architecture | `RunState` container, castle persistence, combat I/O, single application rules (`C-1a`), Game scene unit binding, survivor healing (`C-1~2`), grunt spawning (`D-0b`) | Teammate format sign-off (`C-1a-confirm`), hero spawning (`D-3`), saved run loading, casualty replacement (`C-3`), meta file persistence |
| Test Scenes | `CombatPrototype`, `UnitBattlePrototype`, `ConstructionPrototype`, `SquadCommandPrototype`, `WallCommandPrototype`, `SiegePrototype` | Wall-breaching/aerial enemies, multi-wave sequences, morale routs, full loop integration |
| Art & Performance | Capsule/box placeholder visuals, dual-tier NavMesh benchmark, AI query optimization (`PERF-nav-query`) | Production Synty packs, animations, VFX, full combat performance profiling |

## Test Baselines & Status Conventions

- **Latest integrated baseline (D-4, 2026-11-13)**: EditMode **216/216**, PlayMode **97/97**, 0 compile errors; user manual verification confirmed for D-4 (`Game.unity`).
- **Destination-wall recovery (`BUG-wall-destination-replan`, 2026-11-13)**: Done on `feature/wall-destination-replan`, user requested final commit/integration on 2026-11-13. Based on latest shared develop `e79fd46` (D-0b/D-4 and Docs migration included), full PlayMode **112/112** passed (46.03s). Move/Engage valid-path application retries once next AI update during NavMesh replacement; arrival/stall settings unchanged. Regression matrix 16/16; original runtime 15/16. [History](../history/BUG/BUG-wall-destination-replan.md).
- **Focus-independent PlayMode (`BUG-playmode-input-focus`, 2026-11-13)**: 93/93 unfocused PlayMode passed (37.72s). Intermittent destination-wall failure tracked as `BUG-wall-destination-replan`.
- **PERF-nav-query (2026-11-12)**: 200v200 idle Hold AI mean 83.371 → 1.679 ms (98.0% reduction); standalone performance unmeasured.
- **Status flow**: `Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → `Done` (`Pending Decision` / `Waiting for Prerequisite` for blockers).

## Early M2 Deliverables in M1

- **C-1a (`Done`)**: Pure C# `RunState`, `RunConfig`, `CastleState`, `BattleInput`, `BattleResult` (`feature/run-data-boundary`).
- **C-1~2 (`Done`, `feature/run-scene-connection`)**: Binds Game scene units to run IDs; records results; restores survivor HP.
- **C-4 (`Done`, `feature/stage-rewards`)**: Payouts gold, materials, items, Commander EXP, unlock points per victory.
- **D-2 (`Done`, `feature/build-costs`)**: Build gold deductions, demolition refunds, `Repair All`.
- **D-0 (`Done`, `feature/facility-frame`)**: Courtyard modal pop-ups (F/Esc), input freezing, currency displays (Siege uses construction station).
- **D-5 / D-1 (`Done`, `feature/shop-and-upgrade`)**: `RunState.Shop` seed-rolled once per prep; 50% sell ratio; `TryUpgradeFacility` applies next prep. Out of scope: re-rolls, locked slots, material-cost goods, qty > 1.
- **D-7 (`Done`, `feature/player-equipment`)**: `I` opens player equipment UI (3 generic slots, immediate stat reflection; hero equipment follows D-3).
- **D-0b (`Done`, `feature/run-unit-spawn`) & D-4 (`Done`, `feature/barracks-recruit`)**: Grunt spawning in front of barracks, squad selection/auto-creation, automatic level-based training bonuses (Lv2 +10%, Lv3 +20% HP/attack).

Even with `Q-13` undecided, keep meta, run, and battle lifetimes separate. Do not implement mid-run saves or unconfirmed result policies.
