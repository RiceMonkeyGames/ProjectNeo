# Past Verification History

Last Updated: 2026-11-13 10:45:42 (KST). Historical log of completed milestones, performance benchmarks, and manual playtest records separated from [Context.md](Context.md). Preserves historical completion scope and verification evidence; past test counts and temporary numbers do not represent current baselines or final performance guarantees. Check [ImplementationPlan Section 3](ImplementationPlan.md#3-current-status--starting-point) first for active task status. Agents should only open this file when past rationales or verification evidence are needed, searching by date or task ID.

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


### 2026-11-12: D-5 Shop Buy/Sell and D-1 Facility Upgrades

- Assignee: Hwang Seong-min, branch `feature/shop-and-upgrade` (based on develop `206e524`). Committed (`24ca647`), merged into develop (`681d08d`, 195 EditMode tests passing post-merge), and pushed to remote. Backlog D-5 and D-1 marked `In Progress` / Hwang Seong-min.
- Scope: Implemented both tasks together as an extensible prototype following design specifications, leaving unspecified details open.
- Changes: `RunState` (shop storefront `Shop`, `ShopStockedFor`, `SetupShop`, `ShopSeedFor`, `RestockShopIfNeeded`, `TryBuy`, `TrySell`, `TryUpgradeFacility`), `RunConfig.Economy`/`WithEconomy`, `GameFacilities` (upgrade button, shop stock/stash lists, result feedback), `GameFlowHud` (prep title gold updated per frame, resolving D-2 display delay), `GameFacilitySetup` (economy data, items, shop pools, and updated pop-up), `GameRewardSetup.Item` exposed internally. Assets: `Data/GameFlow/Economy.asset` (50% sell ratio), items `item_wood_shield` (25g), `item_leather_armor` (30g), `item_chain_armor` (60g), `item_steel_sword` (80g), `Facility_Shop.asset` level-based pools (Tier 1: 3 of 4, Tier 2: 4 of 6, Tier 3: 5 of 6). Re-generated `Game.unity` pop-up via Editor API.
- Prototype Decisions: Upgrades do not re-roll current prep inventory, reflecting on the next prep instead. Each stock entry can be purchased once per prep. Selling price considers purchase gold only (per `ItemShop` rules). Tavern, Barracks, and Blacksmith level up in tier without specific perks attached yet. Re-rolling and locked slots deferred.
- Current Limitations: Siege facility has no pop-up and cannot be upgraded. Stash displays up to 8 rows; excess items show count only. Equipping purchased items is handled in D-7.
- Verification: EditMode 195 passed (9 new `RunShopTests`). PlayMode 85 passed (2 new `GameFacilityPlayTests`). Captured shop and blacksmith pop-up visuals in Play Mode, verifying stock, stash, upgrade button, text rendering, and HUD spacing (panel height set to 540). Physical inputs and Windows build not run.
- Manual Verification: Open `Assets/_Project/Scenes/Game.unity` → Play → Face shop building and press F → Left `Stock` lists 3 items (e.g. Iron Sword · 40 gold) → Clicking an item deducts gold and lists it under right `Stash` with `Sell · Half gold` → Clicking it refunds half gold → Click top-right `Upgrade · 60 gold` → Displays `Level 2 / 3` and button changes to `120 gold` (stock increases to 4 on next prep) → Close with Esc, win combat, open shop next prep to see refreshed inventory. Upgrade button operates similarly on other facility buildings. Tuning values located in `Facility_Shop.asset`, `Economy.asset`, and `item_*.asset`. State resets upon exiting Play Mode.

### 2026-11-12: D-7 Player Equipment

- Assignee: Hwang Seong-min, branch `feature/player-equipment` (based on develop `d71f9b6`). Committed (`977878a`), merged into develop (`050948a`, 201 EditMode tests passing post-merge), and pushed to remote. Backlog D-7 marked `In Progress` / Hwang Seong-min. Scope: Player equipment only (hero equipment follows D-3; Q-31 applies to fallen heroes).
- Changes:
  - `RunState.AddHero(Character)`, `TryEquip`, `TryUnequip`.
  - `RunUnitBinding.Create(..., playerCharacter)`: `GameRunLink` passes scene player `Character` so run and scene share the same character instance. `CharacterActor` untouched.
  - `Flow/GameEquipment` (equipment UI).
  - `PreparationInteractionSettings.Equipment` (I), `PreparationInteraction.EquipmentRequested`, `EquipmentKey`.
  - `GameFlowHud`: Exploration prompt displays equipment hotkey; panel prompt generalized to "Esc to Close".
  - `GameFacilitySetup`: Serializes equipment UI, item modifiers, and key bindings.
  - Assets: `Interaction.asset` (equipment key), `Rewards/item_*` equip stat modifiers.
  - `Game.unity`: `EquipmentPanel` and component wiring (saved via Editor API).
- Prototype Decisions: Equipment slots are generic 3 slots per character definition. Max HP modifier omitted for now because equipping mid-prep does not automatically refill current HP. Equipment cannot be changed during combat.
- Verification: EditMode 201 passed (6 new `RunEquipmentTests`). PlayMode 88 passed (3 new `GameEquipmentPlayTests`). Physical inputs and Windows build not run.
- Manual Verification:
  1. Open `Assets/_Project/Scenes/Game.unity` → Play.
  2. Buy equipment (e.g. Iron Sword) from Shop (starts with 100 gold). Close with Esc.
  3. Press I. Equipment panel shows `Equipment Slots 0 / 3` with attack/defense stats, and right `Stash Equipment` lists purchased item.
  4. Clicking the item moves it to left `Equipped` and increases attack (Iron Sword +4) or defense immediately.
  5. Clicking equipped item unequips it, restoring baseline stats.
  6. Close with Esc. I key is disabled during combat.
  7. Modifier values tuned in `Rewards/item_*.asset` Equip Modifiers, hotkeys in `Data/GameFlow/Interaction.asset`. Resets upon exiting Play Mode.

### 2026-11-12: DOC-workflow-branches - Task Branch Lifecycle

- User decision: Select and claim a backlog task, sync latest shared `develop`, then create `feature/<task-description>` and record the branch in the backlog before implementation.
- After user-owned code merge into `develop` is confirmed remotely, delete the corresponding remote task branch. Retain unmerged work. Purpose: Keep branch references manageable across agents. Immediate backlog claim/completion synchronization remains the existing exception to user-owned commits and merges.
- Updated AGENTS.md, Backlog.md, Context.md Section 3, and ImplementationPlan.md Sections 2 and 7. No game rules changed.
- Verification: Documentation consistency and `git diff --check`. No Unity execution or tests required; no actual branches created or deleted for this documentation update.
- Manual verification: Read the workflow in AGENTS.md, Backlog.md, Context.md Section 3, and ImplementationPlan.md Sections 2 and 7. Confirm claim sync -> latest develop -> feature branch -> implementation/verification -> user merge -> confirmed remote integration -> remote task branch deletion. No runtime reset required.

### 2026-11-12: TOOL-claude-read-deny - Claude Code Read Exclusions

- Added shared `.claude/settings.json` with `permissions.deny` `Read(...)` rules to cut token usage across three Claude Code users. `.claudeignore` is not supported by Claude Code, so it was not used.
- Blocked: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `Build(s)/`, `*.csproj`, `*.sln(x)`, `Assets/TextMesh Pro/`, `*.meta`, `*.unity`, `*.prefab`, `*.mat`, `*.shadergraph`, `*.fbx`, `*.blend`, `*.psd`. Kept readable: `.cs`, `.asmdef`, ScriptableObject `.asset`, `ProjectSettings/`, `Docs/`.
- Updated Context.md Section 3 (Work Environments). No game code or Unity assets changed.
- Verification: JSON syntax parse only. Claude Code behavior must be confirmed manually.
- Manual verification: Restart Claude Code at the repository root, run `/permissions` and confirm the deny rules are listed; ask it to read a `.unity` or `.meta` file and confirm it is refused; confirm `.cs` files still read normally.

### 2026-11-12: TOOL-claude-read-deny (revision) - Deny List Review for Claude-Led Development

- Fixed: `Logs/**` block broke the documented compile check (`Logs/Editor.log` search for `error CS`). Now only `*-prev.log`, `AssetImportWorker*`, `*.jsonl` in `Logs/` are blocked.
- `Library/` narrowed to generated subfolders so `Library/PackageCache/` package source can be read by explicit path (gitignored, so not hit by default searches).
- Unblocked `.prefab` and `.mat` (project files are small; needed for reference debugging). Kept `.unity` blocked (`Game.unity` ≈ 596 KB).
- Added pre-emptive blocks for art phase: `Assets/Synty/`, `Assets/Polygon*/`, `Assets/Plugins/`, `Assets/Samples/`, textures under `Assets/`, models, audio, fonts, video, `*.anim`, `*SDF.asset`, `LightingData.asset`, `NavMesh*.asset`, binaries.
- Added `Edit(...)` deny for `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.controller`, `*.anim` to enforce the Editor-API-only rule.
- Verification: JSON parse only. Manual: restart Claude Code, run `/permissions`; confirm `.unity`/`.meta` reads and `.asset` edits are refused, while `.cs`, `.prefab`, and a `Grep` for `error CS` in `Logs/Editor.log` work.

### 2026-11-12: PERF-nav-query - Real AI Benchmark and Search Cost Reduction

- Assignee Codex, branch `feature/nav-query-profile`, based on develop claim `55ee79d`. Claim was committed/pushed before implementation. User confirmed the shared `UnitCombatAI.cs` file had no competing edits and authorized changes. Status: Waiting for Verification; feature changes remain uncommitted/unmerged for user review. No remote feature branch has been published or deleted.
- Diagnosis: No-target Hold and Engage treated null as an invalid target each frame and bypassed DecisionInterval. Idle Hold consequently scanned the entire actor registry every frame even when no enemy was within chase range. This baseline's dominant cost was candidate/range validation, not proven to be NavMesh path queries.
- Fix: Only invalidate an existing target immediately (including destroyed Unity object handles); otherwise respect the existing 0.25s decision interval. Reject same-team/out-of-radius candidates early; for Hold in-reach scans use conservative collider AABB distance before the unchanged exact collider/LoS checks. Retain offset/large colliders. Reuse the valid retained Engage target's reachability path instead of calculating it twice. Move path recalculation and terrain mutation handling remain unchanged.
- Added `UnitAIQueryBenchmark.cs` and Editor-generated .meta, plus 3 UnitCombatPlayTests behavioral tests for delayed empty-target acquisition, immediate death replacement (Hold and Engage), and an offset collider whose root is outside chase radius but body is in range.
- Environment: Unity 6000.6.8f1 Editor, Windows 11, Intel Core i7-14700 (28 logical cores). Each scenario has 30 warm-up and 120 measurement frames at fixed simulated 1/30s. Geometry, soldiers, profile, AI settings and NavMesh are created in a temporary scene using existing factories/Editor-managed import; runtime numerical definitions were not changed. No randomness is used.
- Measurement: Automatic AI updates are disabled and cached delegates invoke the unchanged Update entry point once per simulated frame; Stopwatch measures aggregate AI CPU dispatch only. Engine steering/physics/rendering/waiting are excluded; wall elapsed is separate. These are controlled flat-arena Editor measurements, not full-game FPS, path-query counts or standalone guarantees. No timing pass/fail threshold is used.
- Repeatability: Both before baselines and final results passed twice. 200v200 idle Hold mean was 87.287/83.371ms before, 1.680/1.679ms after (98.0% reduction comparing second runs). Final p95 was 11.973/12.023ms; synchronized decision frames still produce peaks. Contact Hold/Engage/Move showed no material regression in these samples. Table uses the second baseline and second final run:

| Case | Per team | Before AI mean ms | After AI mean ms | Before p95 ms | After p95 ms |
|---|---|---|---|---|---|
| Move | 50 | 0.067 | 0.073 | 0.221 | 0.219 |
| HoldIdle | 50 | 5.343 | 0.162 | 5.996 | 0.866 |
| HoldContact | 50 | 0.554 | 0.553 | 0.619 | 0.584 |
| Engage | 50 | 0.341 | 0.339 | 0.538 | 0.483 |
| Move | 200 | 0.257 | 0.249 | 0.805 | 0.785 |
| HoldIdle | 200 | 83.371 | 1.679 | 86.611 | 12.023 |
| HoldContact | 200 | 2.387 | 2.297 | 2.910 | 2.408 |
| Engage | 200 | 2.065 | 2.006 | 2.810 | 2.532 |

- Verification: Compilation `failed=False compiling=False`; focused UnitCombatPlayTests 12/12 passed; full non-explicit PlayMode suite 91/91 passed (37.85s). Explicit benchmark passed twice before and twice after final optimization (final runs 3.06s/3.11s); an intermediate cadence-only benchmark also passed. EditMode was not rerun because no pure C# rule implementation changed. No standalone build or physical input playtest was performed.
- Cleanup verified after tests: `playing=False capture=0 background=False vsync=0 targetRate=-1`; original Game scene restored. Benchmark teardown owns temporary scene/assets and restores settings. Auto-tick was already enabled before execution. `git diff --check` passed.
- Local measurement files: Logs/benchmark_nav_query_baseline1.csv, baseline2.csv, cadence.csv, final1.csv, and benchmark_nav_query.csv (latest final). These Logs artifacts are ignored by Git; the table above preserves evidence in repository docs.
- Manual/repeat verification: In Window > General > Test Runner > PlayMode select UnitCombatPlayTests and Run Selected. Run the explicit benchmark via the exact Context Section 6 command; inspect Logs/benchmark_nav_query.csv after completion. For actual controls use Assets/_Project/Scenes/Game.unity: Play → F1-F3 select → 3 Hold → F at gate banner to start battle → confirm idle/nearby-enemy response, then 2 Engage and 1 Move on aimed ground; targets should be replaced after death and normal wall routes should remain usable. Stop Play to reset. Game scene is open for this check. New unit-count/terrain stress scenarios, standalone profiling and user-owned commit/merge remain outside verified scope.

### 2026-11-12: PERF-nav-query - User Manual Verification Confirmed

- User replied that verification was complete after the supplied Game scene check procedure. This is user-reported confirmation of that check, not an additional agent-run playtest or standalone performance measurement.
- Backlog status changed from Waiting for Verification to Done. ImplementationPlan updated to record the confirmation; prior automated results remain unchanged (PlayMode 91 passed and repeated before/after explicit benchmarks).
- Feature code and supporting docs remain uncommitted on `feature/nav-query-profile`; only the backlog completion status is synchronized to shared develop under the workflow exception. User-owned feature commit/merge remains pending. The remote feature branch does not exist and is not eligible for cleanup before integration.
- Existing repeat/manual steps remain in Context Section 6. Stop Play to reset runtime state. No new Unity operations were needed for this documentation-only confirmation.
- Backlog-only completion commit `df81940` was pushed to remote `develop` from an isolated clean worktree; latest teammate commits were preserved. Feature code/docs remain untouched and uncommitted in the original worktree.

### 2026-11-12: PERF-nav-query - Final Integration at User Request

- User requested finalization, authorizing feature commit/push, develop integration and task branch cleanup. Implementation commit `22061ac`; latest develop merged into feature as `03c57df`, preserving both documentation histories and teammate access-policy changes. Feature branch published; local develop merge `f7207f3`. A concurrent teammate backlog-only update `251b8e5` was then integrated, preserving other assignees' Done statuses; no additional gameplay changes required another code regression run.
- Integrated regression: An initial attempt launched from the user's still-active PlayMode stalled in transition, was cancelled, and restarted only after `playing=False transitioning=False`. Under default unfocused input routing the full suite finished 88/91: the existing GameEquipment I-open, GameFacility F-open and GameFlow construction F-open tests failed. AI, movement and siege tests passed.
- Diagnosis/check: Focus-independent synthetic input routing (InputSettings IgnoreFocus + AllDeviceInputAlwaysGoesToGameView, background execution on) made GameEquipmentPlayTests pass 3/3 and the full suite pass 91/91 (38.15s). No input/product code was altered. Registered BUG-playmode-input-focus for test fixture isolation; this follow-up is separate from AI optimization.
- Cleanup: Original input routing and background settings restored via Editor APIs. Final restored values: playing=False, background=False, ResetAndDisableNonBackgroundDevices, PointersAndKeyboardsRespectGameViewFocus. Saved restored PlayerSettings through Editor APIs and verified clean git status before merge. No temporary project-setting change is included in the feature.
- Existing user manual confirmation remains valid; benchmark timing is the earlier repeated flat-arena Editor AI-only measurement, not standalone FPS. No further build or physical input test was claimed. Repeat/manual procedure remains Context Section 6, including Stop Play to reset.
- Remote develop push confirmed at `59c262d`. Verified the feature tip was an ancestor of origin/develop, then deleted remote `feature/nav-query-profile` and its merged local branch. Current branch is develop and the working tree was clean. Feature code and supporting docs are now integrated, rather than only backlog status.

### 2026-11-12: Q-04·05 Morale, Rout, and Captain Death Rules

- Assignee: youngho. Doc-only decision task; no code or Unity changes.
- Decided with user: Bannerlord-style squad morale combined with a Darkest Dungeon-style breaking point check. Scale 0–200 (start 150), once-per-battle check at 100 (Collapse or 25% Awaken), whole-squad rout at 0 to a courtyard retreat point, squad-held morale carried between battles (+20 after battle, routed squads restart at 80), starting-below-100 debuff, stat bands, ally/enemy loss causes, Collapse types (Fear, Panic, Contagion), Awaken raises own and nearby squad morale. Enemies: no check or effects, rout at 0, counted as exit alongside Q-30.
- Rejected or deferred: war cry rallying routed units, Berserk, enemy hero/boss approach, explosions, ladder pressure, being surrounded, siege-failure loss for enemies, player presence modifying the check, prep-phase recovery facilities (later design), Last Stand / Steadfast Awaken types (later).
- Recorded in: GameDesign "Ally Morale: Breaking Point" / "Enemy Morale", ImplementationPlan Q-04·Q-05, Backlog Q-04·05 (Done) and B-8.1 (prerequisite resolved).
- Verification: Document review only. Values are starting points for data assets and need playtesting after B-8.1/B-8.2.

### 2026-11-13: BUG-playmode-input-focus - Scoped Synthetic Keyboard Focus Routing

- Assignee Codex; branch `feature/playmode-input-focus`, created from synced develop claim `f99a2cb`. Claim committed/pushed before implementation. User requested finalization on 2026-11-13, authorizing implementation commit and develop integration.
- Added `Assets/Tests/PlayMode/FocusIndependentInputScope.cs`. It snapshots BackgroundBehavior and (Editor only) EditorInputBehaviorInPlayMode, applies IgnoreFocus/AllDeviceInputAlwaysGoesToGameView while each synthetic keyboard test runs, then restores the original values via IDisposable/using on success, assertion failure, or exceptions. Applied only to the I/Esc equipment, F/Esc shop, and F/Esc construction methods; existing keyboard device cleanup remains intact.
- Added `FocusIndependentInputScopeTests.cs`: normal/exception exit restore the same settings object and both field values (2/2 passed). New .meta files were generated by Unity import, not hand-authored.
- Implementation correction discovered by regression tests: swapping a cloned settings object destroys Unity's default HideAndDontSave settings instance and prevents restoring it (initial helper tests 0/2). Final implementation snapshots fields on the existing object; it does not replace settings. Initial experimentation left only an in-memory clone name; restored the original empty name and verified complete EditorJsonUtility JSON equality with the pre-test snapshot (`settingsUnchanged=True`). No persistent input asset or PlayerSettings changes.
- Verification: Unity 6000.6.8f1, compilation failed=False/compiling=False. Helper regression 2/2 passed. First full non-explicit PlayMode 92/93 passed (38.70s): all three keyboard methods passed; ConstructionPlayTests.MoveDirective_ReplansWhenDestinationWallDisappears failed its >1m movement assertion (actual 0.647087097m). Isolated retry 1/1 passed (0.45s); second full PlayMode 93/93 passed (37.72s). Registered BUG-wall-destination-replan rather than changing unrelated combat/navigation code. Game View was deliberately unfocused via ProjectBrowser focus after each full run began; during first run focused window was none. No global input routing override was applied by the validation commands.
- Cleanup: playing=False, Application.runInBackground=False, original input settings JSON identical, Game scene reopened. Auto-tick was already enabled. git diff --check passed. EditMode not rerun (test infrastructure only); standalone build and physical-key playtest excluded.
- Manual verification: Window > General > Test Runner > PlayMode. Run GameEquipmentPlayTests, GameFacilityPlayTests, GameFlowPlayTests and FocusIndependentInputScopeTests; click Project/Inspector while tests execute. Expected: I opens equipment, F opens shop/construction, Esc returns, and both settings-restoration cases pass. In Project Settings > Input System Package confirm original Background Behavior and Play Mode Input Behavior remain after completion. For physical controls, open Assets/_Project/Scenes/Game.unity, Play, press I/Esc; approach shop/construction and press F/Esc. Expected: matching screens open and return to first-person prep. Stop Play Mode to reset; reopen Game after Test Runner cleanup if needed.
- Final integration (2026-11-13, user requested): committed implementation and supporting docs as `c1506ba`, fast-forwarded develop, pushed, and fetched to confirm remote ancestry. Local `feature/playmode-input-focus` deleted after verification; no remote task branch existed. Working tree was clean. No code changed after the recorded 93/93 run, so tests were not repeated for commit/branch operations.
