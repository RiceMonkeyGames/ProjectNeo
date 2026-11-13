# Current Implementation Status & Starting Point

Last Updated: 2026-11-13 16:07:37 (KST)

> Purpose: Active milestone state, implemented vs pending connections, and M2 early deliverables. | Parent: [INDEX](../INDEX.md) | Related: [Milestones](Milestones.md), [Backlog](../Backlog.md), [Scenes](../codemap/Scenes.md)

**Current Milestone: M1 Single Siege Battle in progress; M2 Repeatable Run Loop in progress in parallel.**

Merged `syj`'s Title/Game flow (G-0, G-1), wall stacking / overlapping unit elevation, and `feature/siege-prototype` test scene into `develop`. `Game` scene links 1st-person prep / construction F interaction → hold B for 1 second at current position (FLOW-01) → enemy assault, ladders, actual win/loss → rewards/results. On the M2 side, run data (`C-1~2`), victory rewards (`C-4`), build/repair costs (`D-2`), facility pop-ups (`D-0`), upgrades (`D-1`), shop (`D-5`), player equipment (`D-7`), grunt spawning (`D-0b`), and barracks recruitment/training (`D-4`) are implemented; formal stage/wave layouts, casualty carry-over (`C-3`), tavern (`D-3`), squad org (`D-8`), and blacksmith (`D-6`) remain follow-ups. Wall-destroying enemies, aerial enemies, morale, and siege weapon firing (`Q-21`) remain pending.

## Implemented vs Pending Real Connections

| Domain | Verified Current State | Remaining Integration Scope |
|---|---|---|
| Project Foundation | Unity 6000.6.8f1, URP, 4 assemblies, test runners, Windows build script | Distribution packaging, release verification |
| Stats, Economy, Facilities | Stat modifiers, multi-material wallet, inventory, trade logic, facility pop-ups/upgrades, shop, barracks recruitment/training | Tavern/blacksmith/squad org UIs, crafting workflows |
| Characters & Combat | 1st-person movement, melee attacks, health, grunt stats, projectiles, auto-combat, Game scene siege assault/ladders/outcomes | Wall-destroying/aerial enemies, active skills, consumables, production HUD |
| Meta & Perks | Progression curves, reincarnation conversion, gacha unlocks, perk payloads | Disk persistence, run reward routing, runtime perk injection |
| Squads, Morale, Walls | Squad structures, grid occupancy/rotation, stacking, async NavMesh rebuild, fall damage, weapon mounting/collapse, 1st-person commands | Morale/rout scene wiring (B-8.1 pure rules done (`BattleSquadMorale`); B-8.2 movement/exit, HUD, settings asset pending), siege weapon firing, wall-destroying enemies, construction delays |
| Run Data Architecture | `RunState` container, castle persistence, combat I/O, single application rules (`C-1a`), Game scene unit binding, survivor healing (`C-1~2`), grunt spawning (`D-0b`) | Teammate format sign-off (`C-1a-confirm`), hero spawning (`D-3`), saved run loading, casualty replacement (`C-3`), meta file persistence |
| Test Scenes | `CombatPrototype`, `UnitBattlePrototype`, `ConstructionPrototype`, `SquadCommandPrototype`, `WallCommandPrototype`, `SiegePrototype` | Wall-breaching/aerial enemies, multi-wave sequences, morale routs, full loop integration |
| Art & Performance | Capsule/box placeholder visuals, dual-tier NavMesh benchmark, AI query optimization (`PERF-nav-query`) | Production Synty packs, animations, VFX, full combat performance profiling |

## Test Baselines & Status Conventions

- **Debug encounter tuning (QA-opening-battle-balance, 2026-11-13; Done on `feature/qa-opening-battle-balance`, develop merge pending)**: User requests rough prototype tuning rather than formal stage balancing. Game enemies reduced 28 → 12 (6 ladder, 2 gate breakers, 4 archers), retaining unit stats/outcome rules; generator emits the same roster. Two controlled courtyard observations reached natural victory at 76.27/78.40 simulated seconds; exposed stationary wall player still dies. Final compile passed, GameBattlePlayTests **10/10** (5.23s). User confirmed manual verification and authorized completion/commit/push; standalone unverified. Formal stage design remains B-7.1. [History](../history/QA/QA-opening-battle-balance.md).

- **Current-position battle start (`FLOW-01`, 2026-11-13; dc048b9, Done; develop integration authorized)**: Hold B for 1 second in prep exploration; show hold percentage; cancel on release/modal/focus loss; block construction/facility/equipment screens and held-key restarts. Retire banner F trigger, preserve position/view. Compile passed, EditMode **216/216**, PlayMode **120/120** (67.85s). Agent inspected the live Game hint. Integration onto develop 21aa87b retains ART-01/02; post-integration dotnet passed, but Unity compile readiness timed out with Pipeline unreachable, so the test counts are from before integration. Physical-key playtest and standalone build unverified. [History](../history/FLOW/FLOW-01.md).

- **One-cell stairs (`SIEGE-03`, 2026-11-13; Done on `feature/siege-03`; develop merge pending)**: 1×1 footprint, full 3m rise; 7 initial stairs migrated through Editor APIs across Game and 3 prototypes. Exact 45° ramp admitted with 46° NavMesh/player slope tolerance. New four-rotation Game tests exercise unit/player ascent and descent across two tiers, no player HP loss, and disconnection after ground stair collapse. Full EditMode **216/216**, final full PlayMode **118/118** (53.74s), compile passed. All four initial layouts/landing connections audited. User confirmed stair feel and authorized completion, retaining approved dimensions. Exact manual rotation/tier/squad matrix was not separately reported. Standalone build unverified. [History](../history/SIEGE/SIEGE-03.md).

- **Construction grid display (`BUG-build-grid-lines`, 2026-11-13)**: Initial lines measured ~0.37px; camera-aware width maintains minimum 1.5px while retaining cell centers/spacing. Render matrix 30 combinations passed, full EditMode **216/216**, full PlayMode **114/114** (47.88s). Agent captured continuous grid; user confirmed manual visual recheck on 2026-11-13. Done; standalone build unverified. [History](../history/BUG/BUG-build-grid-lines.md).

- **Construction wheel zoom (`BUG-build-camera-zoom`, 2026-11-13; on `fix/bug-build-camera-zoom`)**: Correct normalized scroll units; red/green actual-input regression reproduced 55 → 54.92556 before fix vs expected 46.75. GameFlow **8/8**, full EditMode **216/216**, full PlayMode **113/113** (46.50s), no compilation failure. User confirmed physical wheel zoom works (2026-11-13); physical wheel manually verified; integrated/pushed to develop in f7400a1 under updated AI Git policy; UI-hover manual check unverified; no standalone build checked. [History](../history/BUG/BUG-build-camera-zoom.md).

- **Latest integrated baseline (D-4, 2026-11-13)**: EditMode **216/216**, PlayMode **97/97**, 0 compile errors; user manual verification confirmed for D-4 (`Game.unity`).
- **Destination-wall recovery (`BUG-wall-destination-replan`, 2026-11-13)**: Done on `feature/wall-destination-replan`, implementation `f2b2d15` integrated/pushed to develop at user request; remote ancestry verified and local task branch deleted (remote task branch never published). Based on latest shared develop `e79fd46` (D-0b/D-4 and Docs migration included), full PlayMode **112/112** passed (46.03s). Move/Engage valid-path application retries once next AI update during NavMesh replacement; arrival/stall settings unchanged. Regression matrix 16/16; original runtime 15/16. [History](../history/BUG/BUG-wall-destination-replan.md).
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
