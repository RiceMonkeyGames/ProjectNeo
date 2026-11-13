# Project Context

Last Updated: 2026-11-13 11:14:44 (KST). All paths are relative to repository root. This document maintains currently executable scenes, code/scene/asset maps, development practices, and verification evidence. Target goals, upcoming tasks, and progress tracking are managed in [ImplementationPlan.md](ImplementationPlan.md); game rules in [GameDesign.md](GameDesign.md); active backlog in [Backlog.md](Backlog.md); required agent rules and documentation entry points in [AGENTS.md](../AGENTS.md).

## Table of Contents

- [1. Current Execution & Verification](#1-current-execution--verification): Immediate playable scenes and controls
- [2. Code, Scene, and Asset Map](#2-code-scene-and-asset-map): Implementation locations and connectivity scope
- [3. Collaboration & Integration](#3-collaboration--integration): Operational standards for team workflow
- [4. Coding & Verification Principles](#4-coding--verification-principles)
- [5. Designing for Art Asset Swaps](#5-designing-for-art-asset-swaps)
- [6. Unity CLI Workflow](#6-unity-cli-workflow)
- [7. Past Verification History](#7-past-verification-history): Milestone results and benchmarks separated into [History.md](History.md)

## 1. Current Execution & Verification

Unity 6000.6.8f1, URP, 3D first-person project. Currently uses placeholder graphics; planned for replacement with Synty assets.

| Scene Path | Verification Target & Controls |
|---|---|
| `Assets/_Project/Scenes/ConstructionPrototype.unity` | Initial build scene. Q/E camera orbit, wall/stairs selection, translucent grid preview, Left-click place, Right-click cancel, R rotate piece. Walking on walls, falling damage via floor destruction. Cannon placement on walls and simultaneous collapse on wall destruction. |
| `Assets/_Project/Scenes/CombatPrototype.unity` | WASD move, mouse look, Left-click melee attack. Dummy combat, health, camera test. Esc to release cursor. |
| `Assets/_Project/Scenes/UnitBattlePrototype.unity` | Automated 10v10 spectator combat (melee & ranged allies vs enemies). |
| `Assets/_Project/Scenes/SquadCommandPrototype.unity` | Direct player control + 3 squad commanding. F1–F3 select squad, ` select all, 1 Move (screen center target), 2 Attack, 3 Hold. Top-left UI displays selection, orders, remaining troops. |
| `Assets/_Project/Scenes/WallCommandPrototype.unity` | Integrated building and squad commanding atop walls. Tab toggles construction / 1st-person, F1–F3 select, 1 Move, 2 Attack, 3 Hold. Construction UI features `Move atop wall` (moves squad 1) and `Destroy foundation` (destroys support under squad 1's lead unit). |
| `Assets/_Project/Scenes/SiegePrototype.unity` | Siege defense prototype. Prep phase (build, demolish, instant squad placement, temp budget) → `Start Battle` → enemy assault (breaches, ladders, gate assault, archers) → win/loss. Tab toggles build/1st-person during prep; locked to 1st-person during battle. Hold F to topple nearby ladders. Press R upon outcome to restart. |
| `Assets/_Project/Scenes/Title.unity` | Actual game entry scene (G-0). `New Game` loads Game scene; `Quit` exits play. |
| `Assets/_Project/Scenes/Game.unity` | Actual game scene (G-0, G-1). Ground 240×300m, castle 96×96m, courtyard 90×90m, 5 placeholder facilities. Middle-click drag pan, scroll zoom, Home reset camera. Prep phase uses 1st-person WASD navigation. Approach construction station and press F → orthographic build view to place walls/stairs/cannons with Q/E rotation; Esc returns to 1st-person. Approach red banner at gate and press F to start battle → 28 enemies advance from north; direct 1st-person combat and F1–F3 / 1 / 2 / 3 squad commanding. Auto outcome on wipe/rout/defeat → `Claim Reward & Next Stage` or `Return to Title`. Opening and playing this scene directly starts at Stage 1 prep. |

Open scenes in Editor and press Play. Exiting Play Mode restores clean state. `*Prototype` scenes are standalone mechanism validation demos; the actual game consists of `Title` and `Game`. Currently, `Game` links construction station interaction, battle trigger, siege combat, actual win/loss, run data, victory reward payout, build/repair costs, facility pop-ups with upgrades, the shop, and player equipment (`I` during prep). Tavern, barracks, and blacksmith features, spawning new units, and formal wave sequencing remain unlinked. Check [ImplementationPlan Section 3](ImplementationPlan.md#3-current-status--starting-point) for latest test pass baselines and remaining connections.

## 2. Code, Scene, and Asset Map

### Runtime Code

Base path: `Assets/_Project/Scripts/Runtime/`. Assembly: `ProjectNeo.Runtime`. Namespace: `ProjectNeo.<Folder>`.

| Folder | Key Classes | Role |
|---|---|---|
| Core | `RandomSource`, `WeightedPool`, `Unlockable` | Seed-based random generator, weighted selection pool, common unlock types and interfaces. |
| Stats | `StatSheet`, `StatValue`, `StatModifier`, `StatType` | Base stats and source-specific modifiers: `(base + flat) * (1 + percent)`. |
| Economy | `Cost`, `Wallet`, `MaterialDefinition` | Costs and wallet storage for gold and various monster material types. |
| Economy | `ItemDefinition`, `Inventory`, `EconomyConfig`, `ItemShop` | Equipment/consumable definitions, inventory containers, sale ratios, shop roll and trade logic. |
| Characters | `HeroDefinition`, `SoldierDefinition`, `SkillDefinition`, `Character` | Hero/grunt/skill data. `Character` stores individual hero stats and equipment; player uses same class. |
| Characters | `PlayerControlSettings`, `PlayerInputController` | Control/camera settings asset, Input System bindings, and CharacterController movement. FirstPersonCamera is default. |
| Characters | `PlayerViewCamera`, `FirstPersonCamera` | Common camera interface and default 1st-person camera. Sensitivity, height, pitch limits driven by PlayerControlSettings. Player visual casts shadow only while active, restored upon deactivation. ThirdPersonCamera preserved but unused. |
| Combat | `Health` | Pure C# health rules. Damage, death, max HP scaling, `RestoreFull` for living units (no resurrecting dead). Stat subscriptions unsubscribed via `Dispose()`. `CharacterActor.SoldierDefinition` reads grunt data. |
| Combat | `CombatProfile`, `CharacterActor`, `CombatTeam` | Body size, attack type, windup delay, min damage, projectile settings asset. Bridges heroes and grunts to common stats; handles teams, targeting, melee/ranged attacks, cooldowns, hit reactions, death. `Character` attached to heroes only; common access uses `Stats`. |
| Combat | `DamageRules`, `CombatPhysics` | Pure C# damage formulas, range, and line-of-sight checks. Attacks enemies in scene; allies do not occlude. Ranged attacks not blocked by shooter's ground object (`GroundUnder`). LoS checks and projectiles share identical rules. |
| Combat | `CombatProjectile` | Linear projectile. Stores damage, team, and direction upon launch; applies defense on hit. Passes through allies, hits first enemy or wall, terminates at max range or lifetime. |
| Combat | `UnitAISettings`, `UnitCombatAI` | Search radius, tick interval, rotation, movement, arrival thresholds. Picks closest reachable enemy, maintains target, updates NavMesh path, melee/ranged attacks, stops upon death. Empty-target Hold/Engage searches respect DecisionInterval; existing invalid targets are dropped immediately. Hold attack candidates use conservative collider bounds before exact range/LoS checks, and valid retained Engage targets reuse the reachability path. Directives (`UnitDirective.Engage` auto-fight / `Hold` defend `HoldPost` / `Move` disregard enemies to destination then Hold). Hold attacks within reach; melee advances to walkable enemies within `HoldEngageRadius` (default 6m), ranged advances within attack range + `HoldRangedChaseExtra` (default 6m) then returns (`HoldChaseRadius`). Ranged stops and fires upon entering range. Move and Attack arrival evaluated when remaining distance <= `ArrivalDistance` (0.35m) or when `MoveStallTracker` detects no progress (`MoveStallProgress` 0.1m) over `MoveStallSeconds` (0.5s) within `MoveStallDistance` (1.5m) of destination. Hold return triggers only if pushed beyond `HoldReturnDistance` (1m); stalls similarly if post is blocked. Default directive without orders is Engage. |
| Combat | `PlaceholderCombatView` | Subscribes to attack, damage, and death events under `Visual` child. Placeholder tinting, attack telegraphs, health bar, death visuals. Contains no combat logic. |
| Facilities | `FacilityDefinition`, `FacilityState` | Per-facility level progression, inventory pools, upgrade costs, and stat modifiers. D-4: `SoldierTraining` (pure C#) swaps the current Training Ground level `soldierModifiers` onto a grunt `StatSheet` under one source, so re-applying never stacks. |
| Squads | `SquadConfig`, `Squad<TUnit>`, `Army<TUnit>` | Squad and army member caps, captain assignments. |
| Squads | `SquadCommander`, `SquadOrder`, `SquadFormation` | Pure C# squad command logic. Single/all squad selection, Move/Attack/Hold orders, default Hold state, arrival transition to Hold, rank-and-file formation offsets. Decoupled from scene and input. |
| Squads | `SquadCommandSettings`, `SquadCommandCenter`, `SquadCommandInput` | Command hotkeys (`InputAction`), formation settings asset, translates orders to unit `UnitDirective` and formation slots, tracks arrival, calculates screen-center raycast. Hotkeys tuned via `Command.asset` inspector. D-0b: `SquadCommandCenter.AddSquad`/`AddMember`/`TryGetGatherPoint` and `SquadCommander.AddSquad` grow squads at runtime; `PlaceholderSquadMarkers` creates discs for new members lazily. |
| UI | `SquadCommandHud` | Test command overlay (selection, orders, headcount, prompts, reticle). Not final HUD. |
| Morale | `SquadMorale` | Squad morale tracker for perk hooks. Actual rout mechanics deferred to B-8. |
| Walls | `ConstructionProgress` | Construction duration and stage pacing calculations. Not yet linked to runtime placement. |
| Walls | `BuildGridDefinition`, `BuildPieceDefinition`, `BuildLayout` | Grid dimensions, `MaxLevels` (default 1), piece footprint, height, HP, and prefab definitions. Pure C# occupancy, rotation, boundaries, fixed gate zone checks, and detached stair connectivity queries. Layer stacking: keyed by `(x, level, z)`, pieces snap atop target column. `BuildPlacement.Level` and `Elevation` track layer and floor height. `Check(..., out level, out elevation)` validates placement; `Remove` followed by `Settle(dropped, unsupported)` collapses or removes unsupported pieces. `At(cell)` reads level 0; `At(cell, level)`, `Top`, `ColumnHeight`, `IsCovered` query vertical layers. |
| Walls | `ConstructionWorld`, `PlacedStructure` | Spawns placement instances and colliders, elevates overlapping living units to new top surfaces without damage, piece health/destruction events, aggregates batch asynchronous NavMesh updates. |
| Walls | `PlacementViewSettings`, `PlacementController`, `BuildGridView` | Translucent preview materials/colors, mouse picking, placement, cancellation, rotation, grid rendering. Preview has zero collision/occupancy/NavMesh impact. |
| Walls | `SiegeWeaponDefinition`, `MountLayout`, `PlacedSiegeWeapon`, `ISiegeWeaponModule` | Unified siege weapon framework. Definition asset (ID, name, size, prefab), pure C# mount point occupancy rules (piece ID + mount ID), shared weapon entity container, extensible behavior interface. No cannon-specific hardcoding. |
| Walls | `BuildCameraSettings`, `ConstructionCameraController` | Orthographic camera angle, size, initial azimuth, rotation step, and transition duration settings. Q/E orbits pivot by 90 degrees while maintaining world/grid coordinates. Game asset enables middle-click drag pan, scroll zoom, Home reset, with tunable start pivot, sensitivity, and zoom clamps. |
| Walls | `FallSettings`, `FallDamageRules`, `UnitFallController` | Height-based fall damage calculation and collapse dropping/landing. Supports NavMeshAgent and CharacterController; restores navigation upon landing. `LiftForConstruction` teleports units harmlessly to new top surfaces upon placement. |
| Walls / UI | `ConstructionSandbox`, `ConstructionHud` | Test scene movement and foundation destruction buttons; uGUI/TMP piece picker and status HUD. |
| Walls | `WallCommandSandbox` | Integrated test scene Tab view toggle. Swaps construction and 1st-person camera, input, and HUD while retaining terrain and orders. Activates units after World builds NavMesh. |
| Flow | `GamePhase`, `BattleOutcome`, `GameFlow`, `RunProgress` | Pure C# flow state machine: Title → Prep → Battle → Reward (if victory) → Next Stage Prep, or Run Results (if defeat) → Title. Illegal transitions return false without state mutation. `RunProgress` maintains current run stage, discarded upon return to Title. |
| Flow | `GameSession`, `SceneCatalog`, `SceneSwitcher`, `GameDirector` | `GameSession.Flow` persists flow state across scene loads, instantiated per session. Scene names defined in `SceneCatalog.asset`. Scene transitions routed exclusively through `SceneSwitcher`. `GameDirector` initiates new run when Game scene is loaded directly and navigates to Title when flow exits. Subscribes via `GameSession.Flow.PhaseChanged`. |
| Flow | `GamePhaseView` | Coordinates cameras, input, and UI per game phase in Game scene. Prep defaults to 1st-person; toggles to build camera/placement/HUD only at construction station; Battle enables 1st-person camera, player input, command input, command HUD, and locks cursor; Reward and Run Results disable both and unlock cursor. Terrain, units, and orders persist across phases. NavMesh rebuilt before unit activation. Replaces `WallCommandSandbox` Tab toggle with formal phase transitions. Hooks construction station F interaction and Esc return. |
| Flow | `PreparationInteractionSettings`, `PreparationInteraction`, `ConstructionStation` | Handles F interaction to open construction station and Esc to exit. Range and input actions configured in `Data/GameFlow/Interaction.asset`. View/input toggled by `GamePhaseView`; prompts/reticle rendered by `GameFlowHud`. |
| Flow | `GameBattleSettings`, `GameBattleController`, `PreparationStation`, `BattleStartStation` | Connects enemy spawning, siege combat, win/loss, and retreat logic to GameFlow Battle phase. Halts AI, attacks, and projectiles upon battle termination; carries over surviving defenders and wall damage to next prep while refreshing test enemy squads. Triggered via F interaction at gate banner. Configuration in `Data/GameFlow/Battle.asset` and `Siege.asset`. Tallies termination cause and kill counts per enemy prefab, passing results to `GameRunLink` (or raw win/loss if unlinked). |
| Flow | `GameRunLink` | Links Game scene to run data (C-1~2, pure C#, `GameBattleController.RunLink`). Constructs `RunState` from scene player and squad layout during initial prep, attached via `GameFlow.AttachRunState`. Captures castle layout into `BattleInput` at battle start; applies `BattleResult` once upon battle end. Heals surviving units via `Health.RestoreFull` during next prep while preserving fallen corpses. `TryRecruit` (D-0b) adds a grunt recruited during prep to both `RunState` (Q-36 squad rule) and `SquadCommandCenter` (auto-creating a scene squad when the run creates one) and binds it via `RunUnitBinding.BindNew`. Does not reconstruct saved runs. |
| Flow | `GameUnitSpawner` | D-0b scene spawning. On the `GameBattleController` object. `Recruit(definition, facility, preferredSquad)` instantiates `SoldierDefinition.UnitPrefab` inactive, applies ally AI/fall settings, places it in front of the facility's `FacilityStation`, then walks it to the squad gather point (empty new squad → Hold in place). `Recruit` charges nothing (tests/scripted spawns); `RecruitAtBarracks` (D-4) follows barracks rules and pays the recruit cost, then calls `GameRunLink.ApplyTraining`. The D-0b F9 debug key was removed in D-4; `debugSoldier` remains as the default grunt for tests. Wired by `ProjectNeo/Setup/Connect Unit Spawning To Game Scene` (`GameUnitSpawnSetup`), which also fills soldier prefab slots from existing squad members. |
| Run | `RunState`, `RunUnit`, `RunHero`, `RunSoldier`, `RunConfig`, `RunSquadSetup` | Pure C# run data container (C-1a). Holds wallet, inventory, heroes, grunts, squads (`Army<RunUnit>`), and castle records. Distinguishes runtime unique unit `Id` from definition asset. Grunts belong to one squad; heroes captain one squad. Unit HP omitted as units fully recover between battles. Starting gold, facilities, and economy settings are configured in a `RunConfig` asset; the Game scene uses `Data/GameFlow/Build/RunStart.asset`. The Game scene builds the player and squads from the scene layout (`GameRunLink`), not from `RunConfig`. Initialized via `GameFlow.StartRun(RunState)` into `RunProgress.State`, discarded on return to Title. |
| Run | `RunUnitBinding<T>` | Pairs scene GameObject units with run unit IDs. Builds `RunState` from scene setup and `BattleResult` from combat survival. Excludes units already removed from run. Decoupled from scene, verified via EditMode. Run player shares `Character` with scene player. |
| Run | `CastleState`, `BattleInput`, `BattleResult` | Records castle placement, layers, rotations, HP, and mounted weapons (`Capture`/`TryRestore` round-tripping with BuildLayout/MountLayout, sorted bottom-up). Battle input stores battle index, stage, player, squads, castle snapshot. Battle result drafts match battle index, win/loss, termination cause, per-unit status (alive/dead/fled), post-battle castle state, and kill counts. `RunState.Apply` verifies battle index to ensure single application; invalid results discarded. Fallen grunts removed; fallen heroes moved to `FallenHeroes` with equipment. Format pending teammate confirmation (Backlog C-1a-confirm). The Game scene creates `BattleInput` and `BattleResult` through `GameRunLink`; the castle stays in the scene, so `CastleState.TryRestore` is not used there yet. |
| Flow | `GameRewardController` | C-4 Victory rewards. Attached to battle controller object. Upon entering Reward phase, evaluates `StageReward` from last victory result and run seed; deposits rewards into run wallet/inventory once, adding Commander EXP and Unlock Points to `GameSession.Meta` (`MetaProgress`). Tuned via `Data/GameFlow/Rewards/StageRewards.asset` and `Meta.asset`. |
| Flow | `GameBuildEconomy` | D-2 Construction and repair economy. Attached to battle controller object. Grants starting gold from `RunStart.asset` in initial prep; prevents unaffordable placements via `PlacementController.ConfirmGate`; deducts cost on placement. Powers `Dismantle` button (click piece, right-click finish), `Repair All` button, and `EconomyStatus` UI. Active during prep phase only. |
| Flow | `GameFacilities`, `FacilityStation` | D-0 Common facility framework. Attached to battle controller object; attaches starting facilities from `RunStart.asset` to run data; opens/closes facility pop-up (`FacilityPanel`: title, level, gold, materials, body, close). Freezes movement/look via `GamePhaseView.EnterPanel` and unlocks cursor. Esc exits via `PreparationInteraction.ExitPanel`; phase changes close panel. `FacilityStation` attached to courtyard building roots for F interaction. Upgrade button (D-1) upgrades any facility by paying cost; Shop facility lists buy/sell rows (cloned from `RowTemplate`) to buy/sell items (D-5). Restocks once per prep if not already stocked. D-4 barracks (`TrainingGround`): the same two columns become recruit rows (`TryRecruit(index)`, name and cost) and squad rows (`ChooseSquad(index)`, -1 = auto first free squad, headcount shown); column headers are swapped at runtime; body shows the current and next training bonus. Upgrading the barracks calls `GameRunLink.ApplyTraining`. Barracks data set by `ProjectNeo/Setup/Configure Barracks Data` (`BarracksSetup`). |
| Flow | `GameEquipment` | D-7 Player equipment UI. Attached to battle controller object. `PreparationInteraction.EquipmentRequested` (press `I` during prep) opens `EquipmentPanel`. Clicking equipped items unequips them; clicking stash items equips them (`RunState.TryEquip`/`TryUnequip`). Displays slot count and stats. Run player shares `Character` with scene player (`GameRunLink` passes to `RunUnitBinding.Create`). |
| Run / Facilities | `RunState.Facilities`, `FacilityDefinition.DisplayName` | Run facility state (`FacilityState`, one per type, initial level 0). Starting facilities set via `RunConfig.Facilities`. Facility definition assets in `Data/GameFlow/Facilities/Facility_*.asset` (prototype values). Materials enumerated via `Wallet.Materials`. `RunState.Shop` (existing `ItemShop`), `RestockShopIfNeeded` (once per prep, shop level pool, `ShopSeedFor`), `TryBuy`, `TrySell`, `TryUpgradeFacility` handle shop and upgrade rules. Sale ratio in `RunConfig.Economy` (`Data/GameFlow/Economy.asset`). |
| Run | `BuildCostTable`, `PieceGoldCost`, `WeaponGoldCost`, `BuildEconomy` | Build cost table asset (piece/weapon gold, past refund ratio, repair gold per HP) and pure C# logic. Payment, prep session logging, refunds (100% current prep, floored ratio past prep), ceiling repair cost calculation, and greedy repair planner (heaviest damaged first). |
| Run | `StageRewardTable`, `KillReward`, `StageReward` | Reward table asset (base/scaling gold, per-enemy bounty gold/materials, item drop chances/counts/pools, EXP, unlock points) and reward payload. `StageReward.Roll` determines rewards deterministically from victory result, stage, and `IRandomSource`. `RunState.ApplyReward` applies reward once per battle; `RunState.Seed` / `SeedFor` generates per-battle seeds. |
| UI | `TitleScreen`, `GameFlowHud`, `RuntimeSystemFont` | Title button callbacks, phase display, building interaction prompt, troop counters, ladder push prompt, result screen, runtime Malgun Gothic font applicator. Displays reward breakdown during Reward phase and gold balance in prep header. Not final UI. |
| Siege | `BuildBudget`, `SiegeOutcomeRules`, `SiegeLadderState` | Pure C# siege rules. Temporary build budget deduction/refund, win/loss evaluation (defeat prioritizes simultaneous victory, Q-09) and retreat trigger, ladder state machine (placed → raising → raised, single climber queue, toppling). |
| Siege | `SiegeSettings` | Settings asset for budget, ladder timings/distances, enemy tick intervals, hotkeys (F topple, Tab view, R restart). |
| Siege | `SiegeLadder`, `PlaceholderLadderView` | Fixed ladder slots along wall perimeter. Active only when wall piece exists at slot. Raised by ground enemies, toppled by defenders atop walls. Blocked from toppling if enemies are actively climbing top. Visualized via placeholder bar under `Visual`. |
| Siege | `SiegeAttackerBrain` | Per-enemy siege decision AI. Delegates to `UnitCombatAI.Engage` if walkable ally exists; otherwise executes role-specific siege behaviors (`SiegeAttackerRole`: Ladder Grunt, Gate Breaker, Archer). Movement and attacks executed via `UnitDirective`. Ladder grunts redirect to gate if no ladders exist. `Retreat()` navigates unit off-map, setting state to `Fled` (excluded from active enemy tally). |
| Siege | `SiegePrepController`, `SiegeDirector` | Prep phase utilities (budget validation, demolition/refund, click-placement of squads) and match coordinator (spawns squads from `SiegeSettings`, orchestrates prep → battle → outcome, camera swaps, enemy activation, win/loss). Not connected to persistent run flow. |
| UI | `SiegeHud`, `SiegeStatusHud` | Test prep overlay, phase banners, troop count, and outcome screens. |
| Meta | `PerkDefinition`, `PerkLoadout` | Perk definitions, stat/special effect payloads, point allocation, application, and resets. |
| Meta | `PerkSpecialEffect`, `MoraleBoostPerkEffect` | Event-driven perk hooks and sample morale boosts. Runtime combat wiring deferred. |
| Meta | `MetaProgress`, `MetaConfig`, `CommanderProgression`, `UnlockGacha` | Persistent meta state, Commander EXP, perk points, reincarnation conversion, duplicate-protected gacha unlock pool. Disk persistence pending. |

### B-2 Combat Prototype Scenes & Assets

- `Assets/_Project/Scenes/CombatPrototype.unity`: Player, 3 test dummies, floor, and collision test wall. Dedicated scene for manual control verification.
- `Assets/_Project/Data/CombatPrototype/`: Player/dummy hero definitions, `CombatProfile`, `PlayerControls`. Adjust temporary values here.
- `Assets/_Project/Prefabs/CombatPrototype/`: Player and dummy prefabs split into common root and `Visual` child. Player input references scene camera, instantiated per scene.
- `Assets/_Project/Art/Placeholder/`: Placeholder materials using capsule and box meshes.
- `Assets/_Project/Input/InputSystem_Actions.inputactions`: Clones `Player/Move`, `Player/Look`, `Player/Attack` actions per player instance. Does not modify shared asset active states.

Controls: Open scene and Play → WASD move, mouse look, Left-click attack. Esc toggles cursor lock; Left-click re-locks. Dummies have 100 HP, player deals 25 damage (dies in 4 hits). Restarting Play Mode restores clean state.

Current melee attack damages closest single enemy passing frontal angle, range, and occlusion checks for `Attack - Defense` (min 1). Player is ally, dummies are enemies. Dummies possess 0 defense. Player features identical health rules via `ReceiveDamage()` but dummies do not retaliate.

### B-3 Unit Battle Scenes & Assets

- `Assets/_Project/Scenes/UnitBattlePrototype.unity`: Spectator automated battle scene. 10 blue allies vs 10 red enemies (7 melee, 3 ranged per side). Includes central obstacle and baked NavMesh.
- `Assets/_Project/Data/UnitBattlePrototype/`: 4 grunt definitions, melee/ranged `CombatProfile`, AI settings, and baked NavMesh data. Values configured for testing; allies tuned stronger.
- `Assets/_Project/Prefabs/UnitBattlePrototype/`: 4 grunt prefabs and linear projectile prefab. Root handles combat, colliders, NavMeshAgent, AI; `Visual` child contains placeholder geometry.

Open scene and press Play to observe combat. Exiting Play Mode resets state. Player controls verified in `CombatPrototype`. Battle outcomes and run progression not yet connected.

`CharacterActor.Configure(...)` must be invoked before activating disabled GameObjects. Activation initializes stats/health and registers unit to target candidate pool; death/deactivation unregisters. AI targets closest enemy with complete NavMesh path. Targeting and path update intervals configured in assets. Units navigate around obstacles if LoS is blocked.

Ranged attacks fire toward target position upon windup completion. Non-homing projectile terminates on first enemy or wall hit. Attack cancels if target dies or escapes during windup. Airborne projectiles continue traversing remaining distance/lifetime regardless of shooter death.

### B-5 Squad Command Scene

- `Assets/_Project/Scenes/SquadCommandPrototype.unity`: 70m × 70m flat terrain with central obstacle. Player, 3 ally squads (5 melee, 5 melee, 4 ranged), 8 distant enemies (6 melee, 2 ranged). Added to end of build settings; first build scene remains `ConstructionPrototype`.
- `Assets/_Project/Data/SquadCommandPrototype/`: `SquadConfig` (4 squads × 10 members), `Command` (keys, rank count, spacing, raycast distance), `AllyAI` (15m vision), `EnemyAI` (12m vision), baked NavMesh. Reuses unit/player prefabs from B-2/B-3.

Controls: Play → F1–F3 select squad (` selects all) → Aim reticle at ground and press 1 to Move. Units do not attack during transit and transition to Hold upon arrival. Press 3 for Hold to defend position, attacking only enemies within reach. Press 2 for Attack to advance toward aimed ground, engaging enemies spotted in transit and resuming march once eliminated. If arrived or no enemies exist, waits in Attack posture. If no ground is aimed, attacks in place.

**Vision:** Unit vision determined exclusively by `UnitAISettings.DetectionRange`. Detects enemies within range having complete NavMesh path. Wall occlusion and FoV angles omitted. Configured as 15m for allies (`AllyAI.asset`) and 12m for enemies (`EnemyAI.asset`). Enemies remain idle until allies enter 12m, enabling phased command testing. Spectator battle `AI.asset` remains 60m.

**Visual Indicators:** `SquadCommand/Visual`'s `PlaceholderSquadMarkers` renders yellow discs beneath living units of selected squads, and cyan discs at Move/Attack destinations. Destination marker persists across selection changes, clearing upon arrival. Temporary read-only visual component; will be replaced by outlines/effects in M5. Dead unit health bars are hidden.

Formations orient toward camera look direction upon order issuance. Unwalkable formation slots redirect to destination origin. Slots exceeding 0.5m height difference are rejected. If path is broken, units halt at partial path end (Q-23). If destination disappears, recalculates within `UnitAISettings.DestinationRecoveryDistance` (4m). Captain heroes and final HUD pending.

### B-5.4 Wall & Command Integration Scene

- `Assets/_Project/Scenes/WallCommandPrototype.unity`: Cloned from construction prototype, connecting 3 squads × 4 units and player atop gate. No enemies; verifies movement and structural collapse.
- Tab toggles between construction and 1st-person command view. Commands use F1–F3, `, 1, 2, 3; player uses WASD, mouse, Left-click, Esc. Per Q-24, combat and flat command scenes adopt 1st-person default.
- `Assets/_Project/Data/WallCommandPrototype/Command.asset`: 2 ranks, 1.1m spacing. Targeting requires surface upward normal >= 0.5 and height delta <= 0.5m from NavMesh, preventing accidental selection of upper/lower tiers when aiming at vertical faces.
- Construction UI `Move atop wall` issues Move command to Squad 1. Tab into 1st-person to observe arrival, or return to construction and press `Destroy foundation` to test falling damage on lead unit. Orders issued mid-fall resume post-landing.

### Siege Defense Prototype Scene

- `Assets/_Project/Scenes/SiegePrototype.unity`: Cloned from `WallCommandPrototype`. Grid 16×20 (48m × 60m); wall line at grid z=8. Cells 4, 11, 12 start breached. 2 stairs, 1 gate (600 HP).
- `Assets/_Project/Data/SiegePrototype/`: `Grid`, `Gate`, `Camera` (scaled orthographic size), `Siege` (`SiegeSettings`), `EnemyRanged` (0.5x upward damage multiplier).
- Temporary budget: 40 starting budget; Wall costs 2, Stairs 3, Cannon 6. Cannons placeable but do not fire.
- Ladder slots at cells 1, 6, 11. Cell 11 requires wall placement before use. Raise: 3s; Topple: 2s; Climb: 2.5s.
- **Adjusting Squads:** Allies and enemies are spawned at runtime per `Siege.asset` (`Ally Squads`, `Enemy Groups`). Each entry defines prefab and headcount. Capped by `SquadConfig` (4 squads × 10 units). Positions and line spacing configured in scene `SiegeDirector`.
- UI troop buttons instantiate dynamically per squad count. Uses `Assets/_Project/Data/SiegePrototype/Command.asset` with F1–F10 hotkeys.
- Squad names reflect composition (`Squad 1 · Melee`, `· Archer`, `· Mixed`).
- Default setup: Allies (4 melee, 4 melee, 4 archer); Enemies (8 ladder grunts, 4 gate breakers, 4 archers) spawning outside castle.
- Enemy prefabs in `Assets/_Project/Prefabs/SiegePrototype/`: `enemy_ladder`, `enemy_gate_breaker`, `enemy_archer`. Ladder grunts carry brown plank; gate breakers carry black shoulder pack.
- Upward damage modifier driven by `CombatProfile` (`upwardShotDamageRatio` default 1, `upwardShotMinimumRise`). Applies when target is significantly higher than shooter.
- Player spawns behind gate on ground.

### B-4 Construction Foundation Scene

- `Assets/_Project/Scenes/ConstructionPrototype.unity`: Initial build scene. Orthographic camera (45 deg azimuth, 30 deg pitch), walls, stairs, fixed gate, test units, piece selection, and collapse test buttons.
- `Assets/_Project/Data/ConstructionPrototype/`: Grid, wall/stairs/gate definitions, preview, fall settings, unit data. Cell dimensions, footprint, height, HP, prefabs, and damage tuned here.
- `Assets/_Project/Prefabs/ConstructionPrototype/`: Unit scale 1 wall, stair ramp, and gate prefabs. Colliders and `PlacedStructure` on root; visuals isolated under `Visual`.
- `Assets/_Project/Art/Placeholder/Construction*`: Placeholder materials and ramp mesh.
- `Assets/TextMesh Pro/`: Core TMP resources imported without examples. Test HUD uses Windows Malgun Gothic at runtime.

Controls: Q/E orbits view by 90 degrees. Select piece → mouse drag preview → Left-click place. R rotates piece by 90 degrees; Right-click cancels. Green indicates valid; red indicates invalid (out of bounds, occupied, fixed gate slot). Overlapping units elevated harmlessly to new top surfaces. Stairs placeable without existing wall connections.

Camera smooths rotation via SmoothStep; continuous inputs chain smoothly without position snapping. Q rotates board left; E rotates board right (inverse of camera orbit sign).

`Move atop wall` commands test unit up stairs to wall. `Destroy foundation` inflicts lethal test damage to supporting piece; unit falls, takes height-based damage, and reconnects to updated NavMesh.

`ConstructionWorld` anchors grid at scale 1. Pieces parented under `Geometry`; previews and gridlines kept separate. NavMesh baked asynchronously. Multi-level stacking active when `MaxLevels` >= 2 (used in `Game` scene with 3 tiers). `ConstructionWorld.Collapse` drops upper pieces (`PieceDropping`), while `UnitFallController` handles falling units. Mounted weapons use mount points from `BuildPieceDefinition.MountPoints`, detached from ground occupancy.

**Siege Weapons (B-4.6):** `Cannon` button → aim at wall → preview snaps to closest mount point → Left-click mount, Right-click cancel. One weapon per mount point. Invalid placements highlight red. `Destroy weapon support` tests concurrent destruction of mounted weapons upon wall collapse. Weapons parented under `Mounts` outside `Geometry`, keeping NavMesh unaffected.

### Editor & Test Scripts

- `Assets/_Project/Scripts/Editor/CombatPrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Combat Prototype`. Generates scenes, prefabs, and assets via Unity Editor APIs.
- `Assets/_Project/Scripts/Editor/UnitBattleSetup.cs`: Menu `ProjectNeo/Setup/Create Unit Battle Prototype`. Generates B-3 scene, prefabs, assets, and NavMesh.
- `Assets/_Project/Scripts/Editor/ConstructionPrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Construction Prototype`. Generates construction scene and assets.
- `Assets/_Project/Scripts/Editor/SiegeWeaponSetup.cs`: Menu `ProjectNeo/Setup/Add Siege Weapons To Construction Prototype`. Adds cannon assets and buttons to construction scene.
- `Assets/_Project/Scripts/Editor/SquadCommandSetup.cs`: Menu `ProjectNeo/Setup/Create Squad Command Prototype`. Generates B-5 scene and HUD. `Upgrade()` adds team vision and markers.
- `Assets/_Project/Scripts/Editor/WallCommandSetup.cs`: Menu `ProjectNeo/Setup/Create Wall Command Prototype`. Creates integrated scene.
- `Assets/_Project/Scripts/Editor/FirstPersonSetup.cs`: Menu `ProjectNeo/Setup/Upgrade Player Views To First Person`. Converts scenes to 1st-person cameras.
- `Assets/_Project/Scripts/Editor/GameFlowSetup.cs`: Menu `ProjectNeo/Setup/Create Game Flow Scenes` creates `Title` and `Game` scenes. `AddCastle()` transfers castle from `WallCommandPrototype`. `ApplyDefaultCastle()` applies 80×100 grid, camera settings, and default wall layout.
- `Assets/Tests/EditMode/GameFlowTests.cs`: Phase transitions, invalid transition rejections, single battle result handling, run reset, phase change events.
- `Assets/Tests/EditMode/RunStateTests.cs`: Start config, unit IDs, squad assignments/caps, captains, battle inputs, death/rout/permadeath handling, single result processing, castle persistence.
- `Assets/Tests/EditMode/CastleStateTests.cs`: Gate, stacking, rotation, HP, mounted weapons persistence, and empty grid restoration.
- `Assets/_Project/Scripts/Editor/GameBattleSetup.cs`: Connects siege battle and banner interaction in Game scene.
- `Assets/Tests/PlayMode/GameBattlePlayTests.cs`: Enemy assault, win/loss evaluation, defeat priority, retreat logic, 3-tier ladders, gate collapse assault, next prep carry-over, run data linking, victory rewards.
- `Assets/_Project/Scripts/Editor/GameRewardSetup.cs`: Menu `ProjectNeo/Setup/Connect Rewards To Game Scene`. Generates materials, items, and reward controllers.
- `Assets/_Project/Scripts/Editor/GameBuildEconomySetup.cs`: Menu `ProjectNeo/Setup/Connect Build Costs To Game Scene`. Connects build costs, demolition, and repairs.
- `Assets/Tests/EditMode/BuildEconomyTests.cs`: Cost queries, payments, refunds, repair rounding, greedy repair planning.
- `Assets/_Project/Scripts/Editor/GameFacilitySetup.cs`: Menu `ProjectNeo/Setup/Connect Facilities To Game Scene`. Generates 5 facility definitions, attaches `GameFacilities` to battle controller, adds `FacilityStation` to 4 courtyard buildings, and creates `FacilityPanel` with upgrade button and shop trade row templates. Generates D-5 `Economy.asset` (50% sell ratio), 4 shop item assets (`Data/GameFlow/Rewards/item_*`), level-based stock pools, and D-7 `EquipmentPanel`, `GameEquipment`, equipment stats, and `I` interaction key.
- `Assets/Tests/EditMode/RunShopTests.cs`: Once-per-prep stocking from level pools, seed determinism, locked item exclusions, buy/sell flow, insufficient funds rejection, upgrade costs, max level limits, and next-prep tier reflection.
- `Assets/Tests/EditMode/RunEquipmentTests.cs`: Equip/unequip stat updates, consumable/non-stash/slot cap rejections, run character sharing with scene player.
- `Assets/Tests/PlayMode/GameEquipmentPlayTests.cs`: Shared Character between run and scene player, actual `I`/`Esc` key input toggles, immediate stat reflection, and battle locking.
- `Assets/Tests/EditMode/RunFacilityTests.cs`: Facility levels, upgrades, duplicate rejections, wallet material queries.
- `Assets/Tests/PlayMode/GameFacilityPlayTests.cs`: Facility pop-up opening/closing, movement freezing, cursor release, battle locking, shop popup buy/sell with stock persistence across same-prep visits, tavern upgrade payments.
- `Assets/Tests/PlayMode/GameBuildEconomyPlayTests.cs`: Starting gold, build deductions, refunds, gate protection, repair execution.
- `Assets/Tests/EditMode/StageRewardTests.cs`: Gold/material calculations, item rolling, seed determinism, single payout validation.
- `Assets/Tests/EditMode/RunUnitBindingTests.cs`: Scene entity to run data binding, duplicate rejection, casualty tracking.
- `Assets/Tests/EditMode/RunRecruitTests.cs`: D-0b/Q-36 recruit squad choice (first free / chosen / auto new squad / cap / battle lock) and `BindNew`.
- `Assets/Tests/PlayMode/GameUnitSpawnPlayTests.cs`: D-0b Game scene recruit spawn in front of the barracks, walk to squad, battle input/commands/result, new squad creation and selection.
- `Assets/Tests/EditMode/BarracksRecruitTests.cs`: D-4/Q-37 barracks recruit rules (recruitable list, cost paid, chosen/auto squad, cap, funds/battle rejection) and non-stacking level training bonus.
- `Assets/Tests/PlayMode/GameBarracksPlayTests.cs`: D-4 Game scene barracks popup recruit with cost and squad choice, insufficient funds, next battle input, upgrade bonus on existing and new grunts (player excluded).
- `Assets/Tests/PlayMode/GameFlowPlayTests.cs`: Scene transitions, phase execution, defeat reset to title, default castle perimeter checks.
- `Assets/_Project/Scripts/Editor/SiegePrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Siege Prototype`. Clones siege prototype scene.
- `Assets/Tests/EditMode/SiegeRulesTests.cs`: Cost deductions, win/loss, ladder mechanics, upward damage reduction.
- `Assets/Tests/PlayMode/SiegePrototypePlayTests.cs`: Budget validation, troop placement, breach entry, ladder climbing, toppling, fall damage, victory/defeat.
- `Assets/_Project/Scripts/Editor/BuildScript.cs`: Windows standalone build script (`Builds/Windows/ProjectNeo.exe`).
- `Assets/Tests/EditMode/`: Stat sheets, wallets, facilities, squads, perks, items, shops, characters, meta progression, health rules.
- `Assets/Tests/PlayMode/CombatPrototypePlayTests.cs`: Movement, collisions, windup, range, LoS, death, camera collision avoidance.
- `Assets/Tests/EditMode/DamageRulesTests.cs`: Defense subtraction, minimum damage, invalid parameters.
- `Assets/Tests/PlayMode/UnitCombatPlayTests.cs`: Targeting, projectile ally penetration, agent death, 10v10 automated combat.
- `Assets/Tests/EditMode/BuildLayoutTests.cs`: Grid bounds, rotated footprints, stair connectivity, gate zones, fall damage curves.
- `Assets/Tests/PlayMode/ConstructionPlayTests.cs`: Camera 4-way orbit, non-snapping interpolation, preview safety, asynchronous NavMesh rebuilds, unit displacement, stair traversal, collapse.
- `Assets/Tests/EditMode/BuildStackingTests.cs`: Vertical stacking, max tiers, stair prohibitions, gate stacking, multi-cell uniform height validation, collapse settling.
- `Assets/Tests/PlayMode/StackedConstructionPlayTests.cs`: 2nd/3rd-tier stacking, multi-tier NavMesh connectivity, unit elevation without damage, collapse settling.
- `Assets/Tests/EditMode/MountLayoutTests.cs`: Mount point vs ground occupancy separation, weapon clearing on piece removal.
- `Assets/Tests/PlayMode/WallCommandPlayTests.cs`: View/input toggling, order persistence, wall traversal, stair loss handling, collapse handling.
- `Assets/Tests/EditMode/SquadCommanderTests.cs`: Selection, orders, arrival transitions, formation geometries.
- `Assets/Tests/EditMode/MoveStallTrackerTests.cs`: Destination stall detection and progress tracking.
- `Assets/Tests/PlayMode/SquadCommandPlayTests.cs`: Squad movement, Hold posture, Attack engagement, screen-center targeting.
- `Assets/Tests/PlayMode/SetupSmokePlayTests.cs`: Basic frame tick verification.
- `Assets/Tests/PlayMode/UnitScaleBenchmark.cs`: Explicit performance benchmark (excluded from standard runs).

## 3. Collaboration & Integration

**Task Branch Lifecycle**: After selecting and claiming a backlog task, sync the latest shared `develop` (including the claim) and create a dedicated `feature/<task-description>` branch from it before implementation. Record the branch name in the backlog. After the user merges the work into `develop` and the merge is confirmed on the remote, delete the corresponding remote task branch. Retain unmerged work branches. This keeps the remote branch list small and makes cross-agent references easier. General code commits and merges remain user-owned.

Active collaboration since 2026-11-11. Teammate manages enemy assault and combat execution; User manages game entry and core flow loop. Assignments follow [ImplementationPlan Section 6.1](ImplementationPlan.md#61-assignment-matrix--shared-modification-rights). Teammates work on separate PCs/branches (Q-26).

### Work Units

- One primary owner per task. Owner is responsible for logic, tests, and completion reporting.
- Task selection and progress are tracked in [Docs/Backlog.md](Backlog.md). Mark `In Progress` with your assignee name and immediately commit/sync before starting; update docs and backlog upon completion. Never modify another assignee's in-progress items.
- Document Task ID, prerequisites, affected files, required I/O interfaces, and verification plans before coding.
- Agree on shared data contracts and call order beforehand. Mock incomplete upstream features with test stubs.
- Sequential coordination required when touching shared files.
- Verify in isolated feature scenes before applying to integrated scenes.

### Work Environments

| Setup | Parallel Allowed | Sequential Required |
|---|---|---|
| Separate PC / Project Clone / Branch | Feature code, unit tests, isolated scenes/assets | Shared contract updates, integration branch merges |
| Shared Folder & Editor | Authoring code/docs in pre-allocated files | Asset creation, scene saves, compilation/refresh, PlayMode, tests, builds, package updates |

Avoid opening a single project folder concurrently across multiple editor instances. Use `--project-path` when multiple editors run on one machine. Only one person operates a shared editor at a time.

**Claude Code read/edit exclusions (token saving)**: Shared `.claude/settings.json` uses `permissions.deny` (Claude Code does not support `.claudeignore`).
- Read blocked: `Library/` except `Library/PackageCache/` (package source lookup allowed by explicit path), `Temp/`, old/worker logs in `Logs/` (`*-prev.log`, `AssetImportWorker*`, `*.jsonl`), `UserSettings/`, `obj/`, `Build(s)/`, `*.csproj`, `*.sln(x)`; third-party folders `Assets/TextMesh Pro/`, `Assets/Synty/`, `Assets/Polygon*/`, `Assets/Plugins/`, `Assets/Samples/`; `*.meta`, `*.unity`, `*.shadergraph`, `*.anim`, `*SDF.asset`, `LightingData.asset`, `NavMesh*.asset`; textures under `Assets/`, models, audio, fonts, video, `*.dll`, `*.pdb`, `*.unitypackage`.
- Read allowed: `.cs`, `.asmdef`, `.prefab`, `.mat`, `.controller`, ScriptableObject `.asset`, `ProjectSettings/`, `Packages/`, `Docs/`, `Logs/Editor.log` (search `error CS` only; do not read whole file), images outside `Assets/` (save verification screenshots outside `Assets/`, `Temp/`, `Logs/`).
- Edit/Write blocked: `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.controller`, `*.anim` (enforces Editor-API-only asset changes).
- Inspect scenes, GUIDs, and serialized references via `script eval` (`EditorSceneManager`, `AssetDatabase.GUIDToAssetPath`). If a Synty pack imports under a different root folder, add that folder to the shared deny list.

### High-Contention Shared Areas

| Area | Policy |
|---|---|
| `CharacterActor.cs`, `UnitCombatAI.cs` | Combat core and movement junction. Managed by primary feature owner; others use adapters or rule helpers. |
| `CombatProfile`, Hero/Grunt definitions, `StatType` | Serialized data contracts. Coordinate field/enum changes in advance; audit dependent assets. |
| Shared prefabs, Integration scenes, NavMesh | Assign explicit owner per task. Never text-merge conflicting asset files. |
| `.asmdef`, `Packages/`, `ProjectSettings/`, Build lists | Managed by integration lead. Verify diffs on auto-modified settings. |
| `GameDesign.md`, `ImplementationPlan.md`, `Context.md` | Managed by doc lead for active task batch. Never write concurrently. |

### Integration Pipeline

1. Validate contracts and owned files for current batch.
2. Develop and verify in isolated workspaces.
3. Integration lead applies changes: Contracts → Rules → Scene wiring → UI / Visuals.
4. Run regression suite upon common code updates. Verify full EditMode/PlayMode and integration scenes before closing batch.
5. Test standalone executable builds during release milestones. Editor passes do not substitute build validation.
6. Record outcomes and unresolved issues; update doc statuses. Commits/merges executed by user.

Always provide manual verification walkthroughs: Exact scene path → Play / run tests → Controls → Expected results → Reset steps. Keep verification scenes open in saved non-playing state.

### Task Handoff Template

```text
Task ID / Title:
Owner / Integration Lead:
Status: Not Started | Pending Decision | In Progress | Waiting for Link | In Verification | Done
Prerequisites / Required Decisions:
Owned Files / Locked Files:
Provided Data/Events / Consumed Data/Events:
Current Task Scope / Deferred Follow-up Scope:
Completion Criteria / Actual Verification Results:
Constraints for Next Owner:
```

## 4. Coding & Verification Principles

- Runtime: `Assets/_Project/Scripts/Runtime/<System>/`, Assembly `ProjectNeo.Runtime`, Namespace `ProjectNeo.<System>`.
- Editor: `Assets/_Project/Scripts/Editor/`, Assembly `ProjectNeo.Editor`.
- Tests: `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`, Namespace `ProjectNeo.Tests`.
- Decouple rules and calculations into pure C# classes; verify via EditMode. Verify movement, scene integration, and destruction via PlayMode and manual play.
- Store numerical parameters in ScriptableObjects. Provide static `Create(...)` factory methods for testing.
- Maintain one class per file for ScriptableObjects and MonoBehaviours; match file name to class name.
- Explicitly assign numeric values to persistent enums; append new entries to the end.
- Route random generation through `ProjectNeo.Core.IRandomSource`. Identical seeds and inputs must yield deterministic results.
- Distinguish runtime instance IDs from static definition asset IDs.
- Never persist editor/scene object references into serialized run state. Include data lifecycles, duplicate reward guards, and event unsubscriptions in integration tests.

## 5. Designing for Art Asset Swaps

Rules for decoupling mechanics from visuals to allow seamless Synty asset drops:

- **Split prefabs into Root (Logic) and `Visual` Child.** Root handles rules, colliders, and navigation; aesthetics belong exclusively to the child GameObject. Swap only the child object.
- **Do not derive colliders or dimensions from meshes.** Unit radius, height, attack range, and grid size are configured via data assets. Mesh scale changes do not affect combat math.
- **Rules never call animations directly.** Logic broadcasts signals ("AttackStart", "Hit", "Death", "SpeedChange"); visual components listen to drive animations, VFX, and SFX.
- **Attack impact timings are data-driven.** Do not rely on animation events. Define hit frame delays in data assets.
- **Assume standard Humanoid rigs for bipeds.** Enables swapping models and animations freely. Pre-configure named empty GameObjects for equip sockets (RightHand, LeftHand, Back).
- **Reference visuals via data assets.** Definitions store references to visual prefabs and icons; code never references prefab names as strings.
- **Wall pieces conform strictly to grid cell dimensions.**
- **Decouple UI layout from graphics.** Centralize fonts, colors, and slice sprites in shared style palettes.
- **Store placeholder assets in `Assets/_Project/Art/Placeholder/`** for easy cleanup. Keep imported 3rd-party packs under root `Assets/` unmodified; place project adaptations in `_Project`.

## 6. Unity CLI Workflow

Interact with open editor instances using Unity CLI 1.0.0-beta.17 (`unity` command) via `com.unity.pipeline`. Check `unity status` to ensure editor is `ready`. Do not conclude editor is closed solely from `unreachable` (verify sandbox local socket restrictions).

Flag `--caller plugin --skill unity-cli` is an invocation tag with no functional side effects.

### Compilation

Editors may pause compilation after external file writes. Request compilation explicitly:

```bash
unity command script eval --caller plugin --skill unity-cli --result-only 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); "requested"'
unity status --until-ready --timeout 30
unity command script eval --caller plugin --skill unity-cli --result-only '"failed=" + UnityEditor.EditorUtility.scriptCompilationFailed + " compiling=" + UnityEditor.EditorApplication.isCompiling'
```

- `unity status --until-ready` may report transient failures during reload; repeat until `editor status` returns `ready`.
- Check compilation errors in `Logs/Editor.log` searching for `error CS`.
- Do not use `unity recompile` or `unity command script recompile` (known to hang or fail in this project).

### Testing

```bash
# Full EditMode suite (synchronous output)
unity command test run --caller plugin --skill unity-cli --mode editor --result-only

# PlayMode suite (runs asynchronously in background; poll status)
unity command editor set_auto_tick --caller plugin --skill unity-cli
unity command test run --caller plugin --skill unity-cli --mode playmode --async-tests --result-only
unity command test status --caller plugin --skill unity-cli --result-only
```

- PlayMode may still be exiting when status reports `completed`. Verify `UnityEditor.EditorApplication.isPlaying == false` via `script eval` before starting EditMode. Starting EditMode mid-transition can lock Test Runner recovery.
- Always execute `editor set_auto_tick` before running PlayMode tests to prevent background frame throttling.
- Use `--filter <substring>` to target specific tests. `[Explicit]` tests require `--include-explicit`.
- `unity test` without `command` is rejected when editor is open.

When validating PlayMode visuals manually, enable background frame execution if necessary:

```bash
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled true --result-only
# Verify visuals and frame ticks; exit PlayMode and restore setting:
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --enabled false --result-only
```


### AI Query Benchmark (PERF-nav-query)

- Source: `Assets/Tests/PlayMode/UnitAIQueryBenchmark.cs`; explicit method `MeasureRealAIAt50v50And200v200`. It creates an isolated flat arena at runtime; no saved scene or production assets are modified.
- Run sequentially in an idle connected editor: `unity command editor set_auto_tick --caller plugin --skill unity-cli`, then `unity command test run --caller plugin --skill unity-cli --mode playmode --filter UnitAIQueryBenchmark --include-explicit --async-tests --result-only`. Poll `unity command test status --caller plugin --skill unity-cli --result-only` until completed; verify PlayMode exited before another operation.
- Output: `Logs/benchmark_nav_query.csv` (overwritten each run). 50v50 and 200v200 Move, idle Hold, contact Hold, and Engage; 30 warm-up + 120 measured frames each at 1/30 simulated seconds. Cached delegates invoke the real AI Update with automatic AI updates disabled. AI time includes dispatch and managed/native calls made by AI, excluding engine NavMesh steering, physics, rendering and frame waiting. Wall elapsed time is a separate column; this is not a standalone FPS result or a count of path queries.
- Teardown restores capture delta, vSync, target frame rate and background execution even on test failure, unloads the temporary scene and destroys generated definitions/NavMesh data. Normal test runs exclude this Explicit benchmark. No performance pass threshold is imposed.
- Input test environment (BUG-playmode-input-focus): The F/I/Esc input methods in `GameEquipmentPlayTests`, `GameFacilityPlayTests`, and `GameFlowPlayTests` use `FocusIndependentInputScope`. It snapshots `InputSystem.settings.backgroundBehavior` and (Editor only) `editorInputBehaviorInPlayMode`, temporarily uses IgnoreFocus/AllDeviceInputAlwaysGoesToGameView, then restores both through `using` disposal, including assertion/exception exits. No PlayerSettings or persistent asset edits are needed. `FocusIndependentInputScopeTests` verifies normal and exceptional restoration. Do not replace the InputSettings object: Unity destroys its default HideAndDontSave instance on replacement.
- Manual input-test verification: Window > General > Test Runner > PlayMode, run `GameEquipmentPlayTests`, `GameFacilityPlayTests`, `GameFlowPlayTests`, and `FocusIndependentInputScopeTests`; click Project or Inspector while they run. Expected: F/I/Esc open/close checks and both restoration cases pass. After exit, verify Project Settings > Input System Package retains its original Background Behavior and Play Mode Input Behavior. Stop Play Mode to reset scene state; reopen `Assets/_Project/Scenes/Game.unity` for normal play.
- Behavioral regressions: `UnitCombatPlayTests.Hold_EmptyTargetSearch_UsesDecisionInterval_AndReacquiresAfterDeath`, `Engage_EmptyTargetSearch_UsesDecisionInterval_AndReacquiresAfterDeath`, and `Hold_FindsEnemyWhoseOffsetColliderIsInRange_WhenRootIsFarAway`.
- Manual check: Open `Assets/_Project/Scenes/Game.unity` → Play → select a squad with F1–F3 → press 3 to Hold while no enemies are near → approach the gate banner and press F to start combat → confirm Hold units engage enemies within their normal radius, then aim at the ground and press 2 for Engage or 1 for Move. Units must still navigate, stop and replace dead targets. Stop Play to reset. Production headcounts, terrain changes and standalone performance need separate validation; historical measurements are in History.md.

### Building

Standalone builds are rejected if the editor is open. Trigger build inside the open editor:

```bash
unity command script eval --caller plugin --skill unity-cli --timeout 1500 --result-only 'ProjectNeo.Editor.BuildScript.BuildWindows(); "done"'
```

Output is generated at `Builds/Windows/ProjectNeo.exe` (git-ignored).

### Packages

```bash
unity command package add --caller plugin --skill unity-package-management --name <package_id> --confirm --wait --result-only
```

### Scenes & Assets

- Do not edit `.unity`, `.prefab`, `.asset`, or `.meta` files directly. Manipulate via Editor APIs (`AssetDatabase`, `PrefabUtility`, `EditorSceneManager`) via `script eval` or run helper `.cs` scripts.
- `.cs` and `.asmdef` files may be authored directly; `.meta` files are generated by editor.
- Empty folders contain `.gitkeep`. Remove `.gitkeep` when adding files to the folder.

### General Caveats

- Shell heredocs containing non-ASCII characters across batch C# file generation have historically caused encoding corruption. Write files individually via dedicated tools.
- Do not merge `2>&1` when parsing CLI output; read stdout only.
- Standalone builds may automatically mutate URP assets under `Assets/Settings/` and files in `ProjectSettings/`.

## 7. Past Verification History

Milestone completions, performance benchmarks, and historical playtest logs are archived in [History.md](History.md). Consult only when researching historical design rationales or validation evidence by date/task ID. Append new completion logs to the bottom of History.md with timestamps, updating current status and maps in Context.
