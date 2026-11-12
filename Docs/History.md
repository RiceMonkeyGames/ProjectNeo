# Past Verification History

Last Updated: 2026-11-12 22:05:21 (KST). Historical log of completed milestones, performance benchmarks, and manual playtest records separated from [Context.md](Context.md). Preserves historical completion scope and verification evidence; past test counts and temporary numbers do not represent current baselines or final performance guarantees. Check [ImplementationPlan Section 3](ImplementationPlan.md#3-current-status--starting-point) first for active task status. Agents should only open this file when past rationales or verification evidence are needed, searching by date or task ID.

New completion results must be appended to the end of this file under `### YYYY-MM-DD: Title`, updating ImplementationPlan Section 3 and Context Section 2 concurrently.

### 2026-11-12: Shared Backlog Operationalization

- Added Development Backlog table (Task ID, Description, Status, Assignee, Completion Criteria) and operating procedures to the top of Google Docs. Preserved existing design text/images below 3 initial empty rows. Verified placement via browser inspection and Markdown export.
- Recorded shared link, claiming workflow, assignee collision protection, post-completion doc sync, and new task logging rules in AGENTS. Reconciled ImplementationPlan Sections 2/7 and Context collaboration guidelines.
- Doc-only modification; no game code, scenes, or assets altered. No Unity compilation, testing, or building run.
- Manual Verification: Open shared doc, press Ctrl+Home in Tab 1. "Development Backlog" header and 5-column table display above existing text. Reversible test: enter temporary task in blank row, verify status/assignee, clear input.
- Follow-up: Populated table with 15 initial tasks based on current plans (playtest verification, C-1a boundary consensus, morale/weapons/enemies decisions, waves, victory tally, casualties, rewards, scouting, facility decomposition, M1 integrated test). All unassigned; distinguished design gaps vs prerequisites.
- Added 5 trigger conditions for updating backlog below table; reflected in AGENTS/ImplementationPlan. Verified 3 pages of table/notes in browser and confirmed exported Markdown body matches pre-edit state.

### Early Work Prior to 2026-11-11

- Project directories, 4 assemblies, EditMode/PlayMode test suites, and Windows standalone build script configured.
- Initial data structures for stats, modifiers, multi-material wallets, costs, facility levels, construction durations, and squad member caps created.

### 2026-11-11: Stage A Data Architecture Completion

| Task ID | Completed Scope |
|---|---|
| A-1 | Perk stats/effects list, targets, event triggers, sample morale boost effect |
| A-2 | Equipment/consumable definitions, stat modifiers, buy price and sell ratio |
| A-3 | Item instances and inventory containers |
| A-4 | Weighted item pools per facility level and seed-based rolling |
| A-5 | Common buy, sell, and stock transaction logic |
| A-6 | Hero and grunt definitions, hero equipment/stats and skill references |
| A-7 | Commander EXP, perk points, unlock points/roster, duplicate protection, guaranteed initial appearance, perk allocation/resets |

64 EditMode tests passed at the time. UI, run loops, disk persistence, runtime skill triggers, and full combat wiring remained pending.

### 2026-11-11: Stage B Combat Foundation Results

#### B-1 Benchmark Results (2026-11-11, Complete)

Method: `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`. Ground 120m × 120m, 4m-high wall segment (12 × 5m pieces), 2 interior ramps. Capsule units with NavMeshAgent traversing ground and ramparts. Averaged over 240 frames in Editor Play Mode.

| Unit Count | Avoidance Quality | Total Frame Time | NavMesh Portion | Physics Portion |
|---|---|---|---|---|
| 100 | High | 1.3 ms | 0.27 ms | 0.12 ms |
| 300 | High | 2.0 ms | 0.50 ms | 0.18 ms |
| 500 | High | 2.9 ms | 0.73 ms | 0.27 ms |
| 500 | Low | 2.9 ms | 0.64 ms | 0.29 ms |
| 1000 | Low | 6.6 ms | 1.26 ms | 0.56 ms |

- At 60 FPS (16.7 ms budget), movement and pathfinding for 500 units consumed ~3 ms.
- Full NavMesh rebuild: initial 22 ms; 11 ms after removing one wall piece. Asynchronous rebuilds took 10 ms across 4 frames.
- Verified units ascending ramps onto ramparts.

**Decision:** Units implemented as standard GameObject + NavMeshAgent. Rebuild NavMesh asynchronously upon wall modifications. Avoid more complex architectures (e.g. DOTS).

**Omissions:** Excluded draw call overhead and character skinning animation costs. Capsule meshes lack animations; profiling hundreds of animated Synty characters must be measured separately once imported.

#### B-2 Results (2026-11-11, Complete)

- Created `CombatPrototype` scene with player, 3 dummies, ground, and collision test wall via Editor APIs.
- WASD move, mouse 3rd-person look, Left-click frontal melee attack. Esc toggles cursor lock. No jump, guard, or directional attacks.
- Player and dummies share `Character` and `HeroDefinition`. Logic cleanly split: `Health` (pure C#), `CharacterActor` (scene combat), `PlayerInputController` (input), `PlaceholderCombatView` (visuals under `Visual` child).
- Attacks damage closest single frontal target after windup delay. Cooldowns, wall occlusion, and attack cancellation on death implemented. Dummy: 100 HP, 25 damage (dies in 4 hits).
- Camera restricts pitch angles and pulls forward to avoid wall clipping.
- **Verification:** 0 compilation errors. 67 EditMode, 6 PlayMode passed. Verified windup, cooldowns, death, line of sight, speed normalization, wall collision, camera collision avoidance.

#### B-3 Results (2026-11-11, Complete)

- `CharacterActor` shares `StatSheet` for heroes and grunts (`SoldierDefinition`). Differentiates teams; excludes dead units from target pools.
- Pure C# `DamageRules` calculates `max(minDamage, Attack - Defense)`. Default minimum damage is 1. Updated dummies to enemy team.
- `UnitCombatAI` navigates via NavMeshAgent, acquiring closest reachable enemy. Maintains target until death or path breakage. Traverses obstacles when LoS is blocked; halts agent upon death.
- `CombatProjectile` moves linearly along firing vector. Penetrates allies; impacts first enemy or wall. Continuous swept collision prevents tunnelling. Ranged and melee share identical damage formulas.
- `UnitBattlePrototype` spectator scene created (10 blue allies vs 10 red enemies: 7 melee, 3 ranged per side).
- **Verification:** 73 EditMode, 15 PlayMode passed. Validated formulas, grunt stats, friendly fire prevention, target switching, projectile ally penetration, obstacle collision, and automated 10v10 battle.

### 2026-11-11: B-4 Construction Foundation & Verification Flow

- Development proceeded solo with generic modular data architecture ahead of Synty pack selection. Manual stair placement, fall damage curves, overlapping unit elevation, translucent previews, Left-click place, Right-click cancel, R rotate established.
- 45-degree orthographic view adopted. Ground 2D grid maintained without cantilevering; ground occupancy decoupled from wall-top mount points.
- Implemented `BuildLayout` occupancy, bounds, gate zones, health, `ConstructionWorld` async NavMesh baking, stair connectivity hints.
- `UnitFallController` handles NavMeshAgent and CharacterController fall damage and recovery. Tested 3m fall dealing ~25 damage (100 -> 75 HP).
- Created `ConstructionPrototype` scene with walls, stairs, gate, test units, and uGUI/TMP controls using system Malgun Gothic font.
- Verified: 88 EditMode, 21 PlayMode passed. Validated placement previews, cancellations, rotation, stair traversal, fall damage, and Windows standalone build. Established rule requiring manual verification walkthroughs in completion reports.

### 2026-11-11: Construction Camera Angle, Orbit, and Transitions

- Lowered pitch angle from 45 to 30 degrees; adjusted orthographic size from 23 to 20 per feedback. Maintained 45-degree diagonal azimuth.
- Added Q/E 90-degree orbit around grid center, decoupled from R piece rotation. Screen picking coordinates and NavMesh preserved across all 4 quadrants.
- Implemented SmoothStep interpolation (0.3s per 90 degrees) driven by unscaled time. Chained inputs accumulate transition duration smoothly without visual snapping.
- Verified: 23 PlayMode passed (including coordinate invariance across 4 view angles, non-snapping interpolation, reverse inputs).

### 2026-11-11: Q/E Rotation Direction Inversion

- Inverted Q/E keys to match screen-space expectations: Q rotates board left, E rotates board right. Smooth interpolation and R piece rotation preserved.
- Verified: 2 camera PlayMode tests passed.

### 2026-11-11: B-4.6 Siege Weapon Framework, Mounting, and Collapse

- Implemented unified siege weapon architecture: `SiegeWeaponDefinition` (data), `MountLayout` (mount rules), `PlacedSiegeWeapon` (shared entity), `ISiegeWeaponModule` (extensible behavior). Cannons exist as data asset and placeholder prefab without dedicated class.
- Added weapon preview, mounting, and concurrent destruction upon supporting wall collapse (`WeaponRemoved` event).
- Scope omissions: firing, damage, targeting, HP, costs (Q-21 pending).
- Verified: 92 EditMode, 25 PlayMode passed. Confirmed cannon placement atop walls and simultaneous destruction when supporting wall collapsed.

### 2026-11-11: B-5.1~5.3 Squad Commanding (Flat Terrain)

- Command keys set to F1–F4 select, ` all, 1 Move, 2 Attack, 3 Hold (Q-11). Move ignores enemies to destination then Holds; Hold attacks enemies within reach; Attack pursues enemies encountered en route; default state is Hold (Q-22).
- Pure C# `SquadCommander` and `SquadFormation` handle logic; `UnitCombatAI.UnitDirective` executes orders; `SquadCommandInput` processes keys and screen-center raycasts.
- Created `SquadCommandPrototype` scene and HUD via Editor APIs.
- Verified: 101 EditMode, 29 PlayMode passed. Validated squad selection, movement, Hold defense, Attack engagement, and UI indicators.

### 2026-11-11: Attack Order Revisions, Vision, and Markers

- Destination reachability rules (Q-23): unreachable targets halt at closest reachable point. Attack orders advance toward point while engaging enemies spotted in sight (Q-22).
- Added `PlaceholderSquadMarkers` rendering yellow selection discs beneath units and cyan markers at destinations. Dead unit health bars hidden.
- Fixed defect: Enemy 12m vision setting was not saved in scene via `SerializedObject`, causing immediate charging. Fixed and added regression test.
- Verified: 101 EditMode, 31 PlayMode passed.

### 2026-11-11: B-5.4 Wall-top Commanding & 1st-Person Test View

- Integrated construction and squad commands in `WallCommandPrototype` (3 squads × 4 units). Connected `Move atop wall` and `Destroy foundation` test buttons.
- Added Tab view toggle between orthographic construction and 1st-person command view while preserving terrain and orders.
- Filtered raycasts to reject wall vertical faces. Explicit path checks halt units at partial path ends upon stair destruction; recalculates destination within recovery radius if target wall disappears. Falling units resume orders post-landing.
- Verified: 101 EditMode, 40 PlayMode passed.

### 2026-11-11: 1st-Person Default Perspective Confirmed

- Q-24 confirmed: Default play perspective switched to 1st-person across `CombatPrototype`, `SquadCommandPrototype`, and `Game` scenes. Construction preserves orthographic view.
- Converted scene cameras and player inputs via Editor APIs. Updated test suites to verify eye-level tracking, pitch clamps, and self-occlusion avoidance.
- Verified: 101 EditMode, 40 PlayMode passed.

### 2026-11-11: Collaboration Kickoff & G-0 Game Flow Scaffolding

- Collaboration established: Teammate handles enemy assault and combat execution; User handles game entry and flow scaffolding. Two-scene architecture (`Title`, `Game`) confirmed; run phases transition inside Game scene without reloading.
- Created `ProjectNeo.Flow` state machine, session containers, scene catalogues, switchers, `TitleScreen`, and `GameFlowHud`. Added `Title.unity` and `Game.unity` to build settings.
- Verified: 109 EditMode, 43 PlayMode passed. Validated Title → Prep → Combat → Reward/Results → Title loop.

### 2026-11-11: G-1 Castle, Construction, and Command Integration into Game Scene

- Transferred castle, construction camera, player, 3 squads × 4 units, and commands from `WallCommandPrototype` into `Game.unity`, linked via `GamePhaseView`.
- Prep defaults to build view, combat to 1st-person command view (temporary placeholder flow). Reused `ConstructionHud` while hiding test buttons.
- Verified: 109 EditMode, 44 PlayMode passed.

### 2026-11-11: Default Castle Layout in Game Scene

- Replaced placeholder layout with formal castle: 24×26 grid, 14×14 outer walls, north main gate, 3 interior stairs, 12×12 courtyard, and northern approach field. Dedicated game grid and camera assets created.
- Verified: 109 EditMode, 45 PlayMode passed.

### 2026-11-11: Layer Stacking (Q-27 Option A) & 2-Tier Stairs

- Implemented Option A: Stacking 3m wall pieces vertically up to 3 tiers. 2-tier stairs placed atop tier-1 walls connect to tier-2. Collapsing lower pieces drops upper pieces down one level.
- Updated `BuildLayout`, `BuildGridDefinition.MaxLevels`, `ConstructionWorld`, `MountLayout` (no weapons on covered pieces), `PlacementController` (auto tier detection), and `UnitFallController`.
- Verified: 118 EditMode, 47 PlayMode passed.

### 2026-11-11: Gate Stacking & Overlapping Unit Elevation

- Confirmed: Walls stackable above main gate. Living units overlapping new placements elevated to top surface harmlessly (`UnitFallController.LiftForConstruction`). Player start position moved to courtyard (12, 8).
- Verified: 122 EditMode, 49 PlayMode passed.

### 2026-11-11: Siege Defense Prototype (Assault, Ladders, Win/Loss)

- Created dedicated `SiegePrototype.unity` scene and `ProjectNeo.Siege` framework. Melee enemies enter breaches or assault gates/climb ladders. Ladders spaced every 5 cells, raised in 3s, toppled by allies in 2s. Archers deal 50% damage shooting upward. Enemy roles: Ladder Grunts, Gate Breakers, Archers.
- Verified: 110 EditMode, 48 PlayMode passed.

### 2026-11-11: Dynamic Squad Roster & Death Visual Cleanup

- Dynamic squad spawning driven by `Siege.asset` (`Ally Squads`, `Enemy Groups`) via `SiegeDirector`. Attached `PlaceholderDeathHider` to hide prop visuals (planks, packs) upon death.
- Verified: 110 EditMode, 50 PlayMode passed.

### 2026-11-11: Dynamic Prep HUD & Expanded Squad Limits

- `SiegeHud` dynamically scales squad buttons to match roster headcount using canvas height. Expanded command hotkeys to F1–F10 via dedicated `Command.asset`. Displayed squad type in labels (`Squad N · Melee/Archer`).
- Verified: 110 EditMode, 50 PlayMode passed.

### 2026-11-12: Siege Branch Handoff & Merge into Develop

- Merged `syj` (`1a6769e`) and `feature/siege-prototype` (`dc08986`) into `develop`. Resolved conflicts, reconciled Q-28–30 IDs, restored occupancy cleanup in `ConstructionWorld.TryDismantle`, and ensured 9 scenes configured in build settings.
- Verified: 131 EditMode, 60 PlayMode passed.

### 2026-11-12: Main Castle Map Expansion & Courtyard Facility Layout

- Expanded map: Ground 240×300m, castle 96×96m, courtyard 90×90m, 80×100 grid. Placed 5 placeholder facility buildings, cross corridors, troop assembly area, and 120m northern approach space.
- Added camera navigation options to `ConstructionCameraController`: middle-click drag pan, scroll zoom (20–180), Home reset.
- Verified: 61 PlayMode passed.

### 2026-11-12: 1st-Person Prep Default & Construction Station Interaction

- Prep phase initialized in 1st-person navigation. Facing courtyard construction station triggers `F · Use Construction`; Esc exits back to 1st-person navigation at matching location/orientation.
- Implemented `PreparationInteractionSettings`, `PreparationInteraction`, `ConstructionStation`, and HUD prompt widgets.
- Verified: 62 PlayMode passed.

### 2026-11-12: Game Scene Siege Combat Integration & HUD Clean-up

- Connected `GameBattleController` to GameFlow Battle phase: 28 enemies advance from north upon interacting with red banner at gate. Collapsing gate triggers upper wall dropping and continued assault on collapsed pieces. 3-tier ladders functional. Defeat prioritizes simultaneous victory. Isolated surviving enemies without walkable paths retreat off-map.
- Restructured HUD into distinct screen quadrants, eliminating overlap.
- Verified: 136 EditMode, 70 PlayMode passed.

### 2026-11-12: Retreat Mechanics for Non-Siege Enemies (Q-30)

- Implemented retreat triggers: If all breach-capable enemies are eliminated and no walkable paths exist to surviving defenders, remaining enemies retreat off-map and flee. Victory declared upon total elimination or flight.
- Verified: 111 EditMode, 52 PlayMode passed on feature branch.

### 2026-11-12: Remote Develop Merge & Combat Unification

- Integrated remote develop (`85869c6`) with local castle expansion and combat wiring (`42222d8`). Preserved teammate's retreat logic while maintaining multi-tier ladder support and Game scene settings.
- Verified: 137 EditMode, 72 PlayMode passed.

### 2026-11-12: Hold Behavior Revisions (Bannerlord Style) & Archer LoS Fix

- Revised Hold directive: Melee units advance to engage walkable enemies within 6m (`HoldEngageRadius`) and return to post. Ranged units shoot within range.
- Fixed archer LoS occlusion: ignored ground collision beneath shooter's feet (`CombatPhysics.GroundUnder`), enabling wall-top archers to fire downward at enemies directly below ramparts.
- Verified: 111 EditMode, 54 PlayMode passed.

### 2026-11-12: Ranged Pursuit During Hold Directive

- Added `UnitAISettings.holdRangedChaseExtra` (default 6m): Ranged units in Hold advance toward walkable enemies within attack range + 6m until within firing range, then halt and fire. Return to post if enemies leave radius.
- Verified: 111 EditMode, 55 PlayMode passed.

### 2026-11-12: C-1a Run Data Architecture & Combat I/O Boundaries

- Author: Hwang Seong-min, branch `feature/run-data-boundary`. Merged into develop.
- Implemented pure C# `RunState`, `RunConfig`, `CastleState`, `BattleInput`, and `BattleResult` in `ProjectNeo.Run`. Attached to flow via `GameFlow.StartRun(RunState)` and `FinishBattle(BattleResult)`. Enforced single result processing. Surviving units fully recover HP; fallen heroes preserved in `FallenHeroes`.
- Verified: 158 EditMode passed; 74/75 PlayMode passed (one pre-existing flaky test).

### 2026-11-12: Timestamp Format Rule Standardized

- Standardized document last updated header to `Last Updated: YYYY-MM-DD HH:MM:SS (KST)`. Enforced updating to exact current time prior to saving across docs and AGENTS.

### 2026-11-12: C-1~2 Run Data Binding in Game Scene

- Author: Hwang Seong-min, branch `feature/run-scene-connection`. Merged into develop (`22253a4`).
- Binds scene GameObject units to run unique IDs. Builds `BattleResult` from combat survival, castle damage, and kill counts. Heals surviving units via `Health.RestoreFull` during next prep while preserving fallen corpses.
- Verified: 163 EditMode passed; 75/76 PlayMode passed.

### 2026-11-12: C-4 Stage Victory Rewards (Prototype Values)

- Author: Hwang Seong-min, branch `feature/stage-rewards`. Merged into develop (`f9f79cf`).
- Implemented `StageRewardTable`, `KillReward`, `StageReward`, and `GameRewardController`. Payouts base/scaling gold, bounty gold/materials per defeated enemy type, equipment drop chances, Commander EXP, and unlock points into run wallet/inventory upon victory.
- Verified: 169 EditMode passed; 76/77 PlayMode passed.

### 2026-11-12: D-3~8 Facility & Organization Task Breakdown

- Author: Hwang Seong-min, branch `feature/facility-plan`. Merged into develop (`1870a3b`).
- Audited codebase and decomposed facility roadmap in ImplementationPlan 4.2.1: D-0 (Framework), D-1 (Upgrades), D-2 (Build Economy), D-0b (Spawning), D-3 (Tavern), D-4 (Barracks), D-5 (Shop), D-6 (Blacksmith), D-7 (Equip), D-8 (Organization). Documented Q-33–36 design gaps.

### 2026-11-12: D-2 Construction & Repair Economy

- Author: Hwang Seong-min, branch `feature/build-costs`. Merged into develop (`1870a3b`).
- Implemented `BuildCostTable`, `BuildEconomy`, and `GameBuildEconomy`. Deducts gold for building walls/stairs/cannons; refunds 100% for current prep pieces, partial ratio for past pieces; single `Repair All` button restores damaged walls proportionally.
- Verified: 175 EditMode passed; 79/80 PlayMode passed.

### 2026-11-12: D-0 Facility Common Framework

- Author: Hwang Seong-min, branch `feature/facility-frame`. Merged into develop (`d146f35`).
- Implemented `GameFacilities`, `FacilityStation`, and 5 facility definitions. Facing courtyard buildings and pressing F opens modal pop-up (title, level, gold, materials, close). Freezes movement/look and unlocks cursor; Esc closes pop-up. Locked during battle.
- Verified: 181 EditMode passed; 83/83 PlayMode passed (all green).

### 2026-11-12: BUG-wall-hold Flaky Stall Fix

- Author: youngho, branch `fix/wall-hold-stall`. Merged into develop.
- Fixed `WallCommandPlayTests.Squad_StopsOnWallWhenStairsDisappear` failure where rear units remained in Move state after stair collapse due to shared destination blocking.
- Implemented pure C# `MoveStallTracker`: Units stalled within 1.5m of destination without 0.1m progress over 0.5s are considered arrived, cleanly transitioning to Hold.
- Verified: 180 EditMode passed, 80 PlayMode passed (6 consecutive isolated passes on the previously flaky test).

### 2026-11-12: Markdown Documentation Translated to English for Token Efficiency

- Translated repository Markdown documents (`AGENTS.md`, `Docs/Context.md`, `Docs/GameDesign.md`, `Docs/ImplementationPlan.md`, `Docs/History.md`) from Korean to English to reduce prompt token consumption.
- Maintained core rules: responses to the user remain in Korean, and timestamp formats are strictly preserved.
- Verified all file links, tables, technical identifiers, and architectural principles remain intact.

### 2026-11-12: Workflow Updated to Repository Markdown Backlog with Immediate Sync

- Migrated the backlog single source of truth from Google Docs to repository Markdown (`Docs/Backlog.md`).
- Updated workflow rules in `AGENTS.md`, `Docs/Backlog.md`, `Docs/Context.md`, `Docs/ImplementationPlan.md`, and `Docs/GameDesign.md`.
- Enforced new rule: When an agent or contributor claims an unclaimed task, they must mark it as `In Progress` with their name in `Docs/Backlog.md` and immediately commit/merge (push/sync) this change before implementing to prevent duplicate assignments and collisions.


