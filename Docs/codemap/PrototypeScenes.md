# Prototype Scenes & Asset Details

Last Updated: 2026-11-16 22:28:48 (KST)

> Purpose: Detailed setup, assets, and controls for prototype scenes (B-2, B-3, B-4, B-5, B-5.4, Siege). | Parent: [codemap/README](README.md) | Related: [Scenes](Scenes.md)

## B-2 Combat Prototype (`CombatPrototype.unity`)

- `Assets/_Project/Scenes/CombatPrototype.unity`: Player, 3 test dummies, floor, and collision test wall. Dedicated scene for manual control verification.
- `Assets/_Project/Data/CombatPrototype/`: Player/dummy hero definitions, `CombatProfile`, `PlayerControls`. Adjust temporary values here.
- `Assets/_Project/Prefabs/CombatPrototype/`: Player and dummy prefabs split into common root and `Visual` child. Player input references scene camera, instantiated per scene.
- `Assets/_Project/Art/Placeholder/`: Placeholder materials using capsule and box meshes.
- `Assets/_Project/Art/Characters/Materials/`: URP Lit copies of Synty character atlas materials (the submodule materials use the Built-in `Standard` shader). Created by `Editor/CharacterModelSetup` ([ART-03](../history/ART/ART-03.md)).
- `Assets/_Project/Input/InputSystem_Actions.inputactions`: Clones `Player/Move`, `Player/Look`, `Player/Attack` actions per player instance. Does not modify shared asset active states.
- Controls: WASD move, mouse look, Left-click attack. Esc toggles cursor lock; Left-click re-locks. Dummies have 100 HP, player deals 25 damage (dies in 4 hits). Restarting Play Mode restores clean state.
- Melee attack damages closest single enemy passing frontal angle, range, and occlusion checks for `Attack - Defense` (min 1). Dummies possess 0 defense and do not retaliate.

## B-3 Unit Battle (`UnitBattlePrototype.unity`)

- `Assets/_Project/Scenes/UnitBattlePrototype.unity`: Spectator automated battle scene (10 blue allies vs 10 red enemies: 7 melee, 3 ranged per side), central obstacle, baked NavMesh.
- `Assets/_Project/Data/UnitBattlePrototype/`: 4 grunt definitions, melee/ranged `CombatProfile`, AI settings, baked NavMesh data (allies tuned stronger).
- `Assets/_Project/Prefabs/UnitBattlePrototype/`: 4 grunt prefabs and linear projectile prefab (root = combat/colliders/NavMeshAgent/AI; `Visual` = placeholder geometry; since ART-03 also a Synty `Visual/Model` with the capsule `Body` renderer disabled). Menu `ProjectNeo/Setup/Apply Character Models` (re)applies the unit-to-model mapping to grunt and siege enemy prefabs; rerun it after any setup menu regenerates those prefabs. It also assigns the locomotion Animator controllers in `Assets/_Project/Art/Characters/Animation/` and the `CharacterModelAnimator` speed driver.
- `CharacterActor.Configure(...)` must be invoked before activating disabled GameObjects. Activation initializes stats/health and registers unit; death/deactivation unregisters. AI targets closest enemy with complete NavMesh path. Ranged attacks fire non-homing linear projectiles on windup completion (cancel if target dies/escapes during windup; airborne projectiles finish path even if shooter dies).

## B-5 Squad Command (`SquadCommandPrototype.unity`)

- `Assets/_Project/Scenes/SquadCommandPrototype.unity`: 70m × 70m flat terrain, central obstacle, player, 3 ally squads (5 melee, 5 melee, 4 ranged), 8 distant enemies (6 melee, 2 ranged). Added to end of build settings.
- `Assets/_Project/Data/SquadCommandPrototype/`: `SquadConfig` (4 × 10), `Command` (keys, rank count, spacing, raycast distance), `AllyAI` (15m vision), `EnemyAI` (12m vision), baked NavMesh.
- Controls: F1–F3 select (` selects all) → aim ground + `1` Move (ignores enemies, Hold on arrival), `3` Hold (defends reach), `2` Attack (advances and engages spotted enemies, resumes march).
- **Vision**: `UnitAISettings.DetectionRange` with complete NavMesh path (wall occlusion and FoV omitted). 15m allies, 12m enemies (`AI.asset` in spectator scene remains 60m).
- **Visual Indicators**: `PlaceholderSquadMarkers` under `SquadCommand/Visual` renders yellow discs under selected living units and cyan discs at destinations. Dead unit health bars hidden.
- Formations orient toward camera look; unwalkable slots redirect to origin; >0.5m height delta rejected. Broken paths halt at partial end (Q-23); disappearing destination recalculates within `DestinationRecoveryDistance` (4m).

## B-5.4 Wall & Command (`WallCommandPrototype.unity`)

- `Assets/_Project/Scenes/WallCommandPrototype.unity`: Cloned from construction prototype, connecting 3 squads × 4 units and player atop gate. No enemies.
- Tab toggles construction / 1st-person command view. Commands: F1–F3, `, 1, 2, 3; player: WASD, mouse, Left-click, Esc (Q-24 1st-person default).
- `Assets/_Project/Data/WallCommandPrototype/Command.asset`: 2 ranks, 1.1m spacing. Targeting requires upward normal >= 0.5 and height delta <= 0.5m from NavMesh.
- Construction UI `Move atop wall` commands Squad 1; `Destroy foundation` tests falling damage on lead unit. Mid-fall orders resume post-landing.

## Siege Defense Prototype (`SiegePrototype.unity`)

- `Assets/_Project/Scenes/SiegePrototype.unity`: Cloned from `WallCommandPrototype`. Grid 16×20 (48m × 60m); wall line at grid z=8. Cells 4, 11, 12 breached. 2 stairs, 1 gate (600 HP).
- `Assets/_Project/Data/SiegePrototype/`: `Grid`, `Gate`, `Camera`, `Siege` (`SiegeSettings`), `EnemyRanged` (0.5x upward damage multiplier).
- Grid supports **3 tiers** (MaxLevels=3, B-6.2 follow-up). Choose Wall and click an existing wall twice to reach 9m; a fourth tier is rejected. WallBreakerSetup also upgrades an existing single-tier grid; no scene YAML edits are needed.
- Budget: 40 starting; Wall 2, Stairs 3, Cannon 6 (cannons non-firing). Ladder slots at cells 1, 6, 11 (11 needs wall). Raise 3s, Topple 2s, Climb 2.5s.
- B-6.2 adds `Prefabs/SiegePrototype/enemy_wall_breaker.prefab`, `Data/SiegePrototype/WallBreaker.asset` and `NearestWall.asset`. Its `Siege.asset` enemy-group count defaults to 0; set it to 1 before Play to exercise the role. Close the three gaps first to force structural attacks; an existing walkable gap makes it use Engage. Restore all edited roster counts after manual checks. Full steps: [B-6.2](../history/B/B-6.2.md).
- Squads spawned at runtime per `Siege.asset` (`Ally Squads`, `Enemy Groups`), capped at 4 × 10; `SiegeDirector` holds positions/spacing. UI troop buttons scale dynamically; F1–F10 hotkeys via `Command.asset`; labels show composition (`Squad 1 · Melee`).
- Default setup: Allies (4 melee, 4 melee, 4 archer); Enemies (8 `enemy_ladder` w/ brown plank, 4 `enemy_gate_breaker` w/ black pack, 4 `enemy_archer` in `Prefabs/SiegePrototype/`). Player spawns behind gate. Upward damage modifier via `CombatProfile` (`upwardShotDamageRatio`, `upwardShotMinimumRise`).

## B-4 Construction Foundation (`ConstructionPrototype.unity`)

- `Assets/_Project/Scenes/ConstructionPrototype.unity`: 45 deg azimuth, 30 deg pitch orthographic camera, walls, stairs, fixed gate, test units, UI controls.
- `Assets/_Project/Data/ConstructionPrototype/`, `Prefabs/ConstructionPrototype/`, `Art/Placeholder/Construction*`, `Assets/TextMesh Pro/` (runtime Windows Malgun Gothic font).
- Controls: Q/E orbits 90 deg (SmoothStep, Q rotates board left / E right); Left-click place, Right-click cancel, R rotate 90 deg. Green valid / red invalid. Overlapping living units elevate harmlessly. Stairs placeable without wall connection.
- `Move atop wall` / `Destroy foundation` verify fall damage and async NavMesh rebuild. `ConstructionWorld` anchors scale-1 grid (`Geometry` parent; previews separate; `MaxLevels` >= 2 enables stacking, 3 tiers in `Game`). `Collapse` drops upper pieces (`PieceDropping`); `UnitFallController` handles falls.
- **Siege Weapons (B-4.6)**: `Cannon` button snaps preview to `BuildPieceDefinition.MountPoints` (parented under `Mounts` outside `Geometry`, NavMesh unaffected). `Destroy weapon support` tests simultaneous weapon destruction on wall collapse.
