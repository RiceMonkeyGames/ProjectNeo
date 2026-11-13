# Editor Setup & Test Scripts

Last Updated: 2026-11-13 23:03:41 (KST)

> Purpose: Map of Editor setup menus, build scripts, and EditMode/PlayMode test suites. | Parent: [codemap/README](README.md) | Related: [UnityCLI](../workflow/UnityCLI.md), [Status](../plan/Status.md)

## Editor Scripts (`Assets/_Project/Scripts/Editor/`)

- `CombatPrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Combat Prototype`. Generates B-2 scene, prefabs, and assets.
- `UnitBattleSetup.cs`: Menu `ProjectNeo/Setup/Create Unit Battle Prototype`. Generates B-3 scene, prefabs, assets, and NavMesh.
- `ConstructionPrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Construction Prototype`. Generates B-4 construction scene and assets.
- `SiegeWeaponSetup.cs`: Menu `ProjectNeo/Setup/Add Siege Weapons To Construction Prototype`. Adds cannon assets and buttons. Menu `ProjectNeo/Setup/Apply Cannon Health (Q-21)` sets `Cannon.asset` HP to 150 (idempotent).
- `CannonSetup.cs` (`SIEGE-02`): Menu `ProjectNeo/Setup/Connect Cannon Firing (SIEGE-02)`. Idempotently creates `Data/ConstructionPrototype/CannonSettings.asset`, `Prefabs/ConstructionPrototype/CannonShell.prefab` (placeholder ball + blast view), adds/configures `CannonModule` on `Cannon.prefab`, creates `Data/GameFlow/CannonOperator.asset`, and wires `CannonOperator` on the Game player and `GameFlowHud`. Tests: EditMode `CannonRulesTests`; PlayMode `CannonPlayTests` (ConstructionPrototype), `CannonAttackPlayTests` (SiegePrototype), `GameCannonPlayTests` (Game).
- `WallBreakerSetup.cs`: Menu `ProjectNeo/Setup/Add Wall Breaker Assets`. Idempotently creates target-strategy/settings assets and an inactive wall-breaker prefab, ensures SiegePrototype grid supports at least three tiers, then registers the prefab in its roster at count 0. Existing positive counts/configuration are retained.
- `SquadCommandSetup.cs`: Menu `ProjectNeo/Setup/Create Squad Command Prototype`. Generates B-5 scene and HUD (`Upgrade()` adds team vision and markers).
- `WallCommandSetup.cs`: Menu `ProjectNeo/Setup/Create Wall Command Prototype`. Creates B-5.4 integrated scene.
- `FirstPersonSetup.cs`: Menu `ProjectNeo/Setup/Upgrade Player Views To First Person`. Converts scenes to 1st-person cameras.
- `GameFlowSetup.cs`: Menu `ProjectNeo/Setup/Create Game Flow Scenes` creates `Title` and `Game` scenes (`AddCastle()`, `ApplyDefaultCastle()` for 80×100 grid, camera, default wall layout).
- `GameBattleSetup.cs`: Connects siege battle and gate banner interaction in Game scene.
- `GameRewardSetup.cs`: Menu `ProjectNeo/Setup/Connect Rewards To Game Scene`. Generates materials, items, and reward controllers.
- `GameBuildEconomySetup.cs`: Menu `ProjectNeo/Setup/Connect Build Costs To Game Scene`. Connects build costs, demolition, and repairs.
- `GameFacilitySetup.cs`: Menu `ProjectNeo/Setup/Connect Facilities To Game Scene`. Generates 5 facility definitions, `GameFacilities`, `FacilityStation` on 4 courtyard buildings, `FacilityPanel`, D-5 `Economy.asset` (50% sell), 4 shop items (`Data/GameFlow/Rewards/item_*`), level stock pools, and D-7 `EquipmentPanel` / `GameEquipment` / `I` key.
- `SquadOrganizationSetup.cs`: Menu `ProjectNeo/Setup/Connect Squad Organization To Game Scene`. D-8 `OrganizationPanel` (UI-01: horizontal card scroll, card template with Header button + List, row template) (reuses `GameFacilitySetup` popup helpers) and `GameSquadOrganization` on the battle object; marks `Interaction.asset` dirty to save the new `O` key.
- `TavernSetup.cs`: Menu `ProjectNeo/Setup/Create Tavern Heroes`. D-3 hero assets in `Data/GameFlow/Heroes/` (swordsman/archer tier 1 60g, knight tier 2 120g, paladin tier 3 200g; grunt prefabs as bodies) and `Facility_Tavern.asset` level hero pools (only when empty). No scene edits.
- `GameUnitSpawnSetup.cs`: Menu `ProjectNeo/Setup/Connect Unit Spawning To Game Scene`. Wires `GameUnitSpawner` and fills soldier prefab slots.
- `BarracksSetup.cs`: Menu `ProjectNeo/Setup/Configure Barracks Data`. Configures `Facility_TrainingGround.asset` and `allied_melee`/`allied_ranged` recruit costs/names.
- `SiegePrototypeSetup.cs`: Menu `ProjectNeo/Setup/Create Siege Prototype`. Clones siege prototype scene.
- `BuildScript.cs`: Windows standalone build script (`Builds/Windows/ProjectNeo.exe`).

## EditMode Tests (`Assets/Tests/EditMode/`)

- General Stage A: Stat sheets, wallets, facilities, squads, perks, items, shops, characters, meta progression, health rules.
- `DamageRulesTests.cs`: Defense subtraction, minimum damage, invalid parameters.
- `BuildLayoutTests.cs`: Grid bounds, rotated footprints, stair connectivity, gate zones, fall damage curves.
- `BuildStackingTests.cs`: Vertical stacking, max tiers, stair prohibitions, gate stacking, multi-cell uniform height, collapse settling.
- `MountLayoutTests.cs`: Mount point vs ground occupancy separation, weapon clearing on piece removal.
- `SquadCommanderTests.cs`: Selection, orders, arrival transitions, formation geometries, runtime `AddSquad`/`RemoveSquad`.
- `RunOrganizationTests.cs`: D-8 grunt moves, emptied-squad removal with captain to reserve, new squad cap, reserve-only captains, player blocked (Q-32 interim), battle lock.
- `RunTavernTests.cs`: D-3 seeded tavern offers, locked/hired/fallen exclusion, gold hire, captain swap to reserve, shared character, battle input leader.
- `MoveStallTrackerTests.cs`: Destination stall detection and progress tracking.
- `SiegeRulesTests.cs`: Cost deductions, win/loss, ladder mechanics, upward damage reduction.
- `MoraleRulesTests.cs`: B-8.1 morale: proportional/fixed losses, ally-only gate/wall, once-per-battle check (collapse/awaken, chance bonus), start-below-100 weak collapse, rout once at 0, Rally/Last Stand/Steadfast, attack bands (enemy low band only), Fear/Panic/Contagion strength, dispose, post-battle recovery max(+20, 80). B-8.2: squad at 150 routs exactly at 90% battle-start deaths (5/10/20 members).
- `RunUnitBindingTests.cs` (B-8.2 additions): `Fled` fate with death precedence, per-type enemy retreats separate from kills, run squad `Morale` survives result apply.
- `GameMoralePlayTests.cs` (PlayMode, Game scene, B-8.2): member death/nearby wall/gate morale loss; routed squad ignores orders, exits at the retreat point (south of castle), drops from `AlliesAlive`, records `Fled` (exited and still running), recovers to 80, reappears next prep and rejoins, next battle starts shaken at 80; all squads exited with gate intact keeps battle running.
- `GameFlowTests.cs`: Phase transitions, invalid rejections, single battle result handling, run reset, phase change events.
- `RunStateTests.cs`: Start config, unit IDs, squad caps, captains, battle inputs, death/rout/permadeath, single result processing, castle persistence.
- `CastleStateTests.cs`: Gate, stacking, rotation, HP, mounted weapons persistence, empty grid restoration.
- `StageRewardTests.cs`: Gold/material calculations, item rolling, seed determinism, single payout validation.
- `BuildEconomyTests.cs`: Cost queries, payments, refunds, repair rounding, greedy repair planning.
- `RunFacilityTests.cs`: Facility levels, upgrades, duplicate rejections, wallet material queries.
- `RunShopTests.cs`: Once-per-prep stocking, seed determinism, locked exclusions, buy/sell, upgrade limits, next-prep reflection.
- `RunEquipmentTests.cs`: Equip/unequip stat updates, slot cap/consumable rejections, run character sharing with scene player.
- `RunUnitBindingTests.cs`: Scene entity to run ID binding, duplicate rejection, casualty tracking.
- `RunRecruitTests.cs`: D-0b/Q-36 recruit squad choice (first free / chosen / auto new / cap / battle lock) and `BindNew`.
- `BarracksRecruitTests.cs`: D-4/Q-37 barracks recruit rules and non-stacking level training bonus.

## PlayMode Tests (`Assets/Tests/PlayMode/`)

- `CombatPrototypePlayTests.cs`: Movement, collisions, windup, range, LoS, death, camera collision avoidance.
- `UnitCombatPlayTests.cs`: Targeting, projectile ally penetration, agent death, 10v10 combat, DecisionInterval/offset collider regressions.
- `ConstructionPlayTests.cs`: Camera 4-way orbit, non-snapping interpolation, preview safety, async NavMesh rebuilds, unit displacement, stairs, collapse. Destination-collapse Move/Engage covers rebuild start delays 0–7 frames (16 cases), retaining >1m movement and requiring <0.6m ground arrival.
- `StackedConstructionPlayTests.cs`: 2nd/3rd-tier stacking, multi-tier NavMesh connectivity, unit elevation without damage, collapse settling.
- `WallCommandPlayTests.cs`: View/input toggling, order persistence, wall traversal, stair loss handling, collapse handling.
- `SquadCommandPlayTests.cs`: Squad movement, Hold posture, Attack engagement, screen-center targeting.
- `SiegePrototypePlayTests.cs`: Budget validation, troop placement, breach entry, ladder climbing, toppling, fall damage, victory/defeat.
- `WallTargetStrategyTests.cs` (EditMode): Nearest selection, stable tie resolution, empty/invalid candidate distances.
- `WallBreakerPlayTests.cs`: Saved wall-breaker prefab attacks/breaches walls, includes gates, selects stacked-column bottom and settled upper piece, assaults existing gaps, holds/retries inaccessible bases, and stops structure damage on retreat. Stacking asserts and uses the actual saved SiegePrototype three-tier grid; a substitute world must not hide scene configuration failures.
- `GameFlowPlayTests.cs`: Scene transitions, phase execution, defeat reset to title, default castle perimeter checks.
- `GameBattlePlayTests.cs`: Enemy assault, win/loss, defeat priority, retreat, 3-tier ladders, gate collapse assault, next prep carry-over, run link, rewards.
- `GameBuildEconomyPlayTests.cs`: Starting gold, build deductions, refunds, gate protection, repair execution.
- `GameFacilityPlayTests.cs`: Facility pop-ups, movement freezing, cursor release, battle lock, shop buy/sell stock persistence, tavern upgrades.
- `GameEquipmentPlayTests.cs`: Shared Character, `I`/`Esc` input toggles, immediate stat reflection, battle lock.
- `GameUnitSpawnPlayTests.cs`: D-0b Game scene recruit spawn in front of barracks, walk to squad, battle input/commands/result, new squad creation.
- `GameBarracksPlayTests.cs`: D-4 Game scene barracks popup recruit with cost/squad choice, insufficient funds, battle input, upgrade bonus on existing/new grunts.
- `GameOrganizationPlayTests.cs`: D-8 Game scene organization window moves a grunt to a new squad and back via cards (UI-01), hired hero to captain, to another squad and back to reserve, (run + scene squads + commander stay aligned, emptied squad removed); closed during battle.
- `GameTavernPlayTests.cs`: D-3 Game scene tavern hire as captain and reserve, shared scene/run character, equipment hero selector, reserve hero deactivated in battle.
- `FocusIndependentInputScopeTests.cs`: Normal/exception restoration of InputSettings via `FocusIndependentInputScope`.
- `SetupSmokePlayTests.cs`: Basic frame tick verification.
- `UnitScaleBenchmark.cs` & `UnitAIQueryBenchmark.cs`: Explicit `[Explicit]` performance benchmarks (excluded from standard runs).
