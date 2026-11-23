# SIEGE-18 | 2026-11-23 | SIEGE - Game 7.5m Grid & Default Castle Layout

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-18 - Game 7.5m Grid & Default Castle Layout

- **Assignee**: `youngho` | **Branch**: `feature/siege-18` | **Claim commit**: `ba488c0`
- **Dependency**: before the claim, `feature/siege-35` was merged into `develop` (merge `b6ac623`) at the user's request, because SIEGE-18 builds on the 7.5m spike ([SIEGE-35](SIEGE-35.md)).
- **Decisions (user, 2026-11-23)**; recorded in [WallsAndSiege](../../design/WallsAndSiege.md) (map scale standard) and [Decisions-Resolved](../../plan/Decisions-Resolved.md):
  - Ground: 32 × 40 cells of 7.5m (240 × 300m).
  - Castle: 13-cell ring (97.5m), courtyard 82.5m, north approach 120m.
  - Gate: middle cell of the north wall (x 16), 3.75m east of the ground centre line.
  - Wall per-cell HP and cost: 1.5× the 5m-piece values (HP 150, 15 gold).
  - Ladder lanes every 2 cells (15m).
  - The 3m prototype scenes are regenerated on 7.5m (not retired).
- **Scope**: placeholder visuals kept. Other kit work stays in its own tasks: kit dressing `SIEGE-19`, kit battlements `SIEGE-20`, ladder lengths `SIEGE-21`, kit gate and machicolation sizes `SIEGE-22`, bastion resize `SIEGE-23`, balance `SIEGE-25`.
- **Implementation**:
  - Runtime `Walls/BuildPieceDefinition`: `maxStorey` (0 = grid limit) and `SetMaxStorey`. `Walls/BuildLayout`: `PlacementIssue.StoreyCap = 16`; `StoreyLimit` also caps `ValleyFill`. `UI/ConstructionHud` shows a message for `StoreyCap`.
  - `Run/RunSaveStore`: `CurrentVersion = 2`, `MinimumVersion = 2`. Version 1 run saves hold 3m cell coordinates and are no longer read.
  - `Combat/UnitAISettings.destinationRecoveryDistance` default 4 → 6 (units ordered onto a one-storey wall that collapsed must reach the ground 5m below).
  - `Flow/GameBattleSettings.spawnCell` and `Stages/SpawnPointDefinition.cell` defaults (40, 90) → (16, 36). `Editor/StageSetup.SideOffsetCells` 15 → 6 (same metres).
  - `Editor/Grid75Migration` (new), one-shot, refuses to run if the grid is already 7.5m. Menu `ProjectNeo/Setup/Migrate To 7.5m Grid (SIEGE-18)`.
    - Shared pieces in `Data/ConstructionPrototype` keep their GUIDs: Wall h5, HP 150, maxStorey 3; Stairs h5, maxStorey 3, with new `Prefabs/ConstructionPrototype/StairsNarrow.prefab` (5m wide, centred; mesh `Data/ConstructionPrototype/StairsRampNarrow.asset`); Bastion h5, maxStorey 3; Gate 1×1 h5, maxStorey 1 (also `Data/SiegePrototype/Gate.asset`).
    - Fall settings: safe height 1m, 6 damage per metre (one 5m storey = 24, three storeys = 84).
    - Prototype grids use 7.5m cells; prototype build cameras ×2.5; `BuildCosts` wall 15 gold; `Battle.asset` spawn cell and the three `SpawnPoint*` assets ×0.4.
    - Prototype scenes (ConstructionPrototype, WallCommandPrototype, SiegePrototype): world and ground resized from the grid; the old 2×1 gate's east cell filled with a wall seed; metre-placed roots scaled ×2.5 horizontally and ×5/3 vertically; SiegeDirector spawn points scaled.
    - `ConfigurePieces`, `ConfigureGate`, `ConfigureFall` and `WidenCamera` are reused by fresh prototype setups.
  - Menu `ProjectNeo/Setup/Rebuild Verification Scenes From Siege Prototype (SIEGE-18)` reruns the EnemyComposition, Flyer and Machicolation verification setups. Flyer and Machicolation scenes and settings get new GUIDs because their setups delete and recopy them; nothing referenced the old GUIDs.
  - `Editor/GameFlowSetup`: constants 32 × 40, `MaxLevels` 4, cell 7.5, ring x 10–22 and z 11–23, gate x 16. Grid `forbidValleys` and `groundStairsOnly` on.
    - `DefaultSeeds`: one-storey ring; gate plus fixed wall = two-storey gatehouse; ground stairs at (13, 22) and (19, 22) rot 0, and (16, 12) rot 2.
    - Courtyard repositions existing facilities by name instead of deleting and recreating them, so components added by other setup menus are kept. New metre positions.
    - Squads and player start positions updated.
  - `Editor/HeroHallSetup.BuildingPosition` (150, 0, 97).
  - `Editor/GameBattleSetup`: `LadderSpacing = 2`; gate skipped by footprint (4 ladders at x 12, 14, 18, 20); battle start station at gate + (1.5, −1.5) cells.
  - `Editor/SyntyGridSpikeSetup.NarrowStairs` extracted as a shared helper. Fresh-build `ConstructionPrototypeSetup`, `SiegePrototypeSetup` and `WallCommandSetup` use the 7.5m values.
  - Tests:
    - New EditMode `BuildStoreyCapTests` (5). `RunSaveTests` checks that version 1 is unreadable.
    - PlayMode prototype tests updated for 7.5m geometry: `WallCommandPlayTests` (wall top from definition height; at most 4 ranks across the 7.5m walkway); `SiegePrototypePlayTests` (melee enemy 4m from post; archer target about 15m from the outer face, because the walkway centre cannot see closer under a 5m wall; ladder push wait 1800 frames; far sortie point (50, 30); breaker test waits for the breaker to reach the gate).
- **Automated Verification**:
  - Code rules check passed (0 violations; 30 serialized assets changed, all through Editor APIs: the migration menu and setup menus).
  - EditMode `836/836` PASSED.
  - PlayMode `339` total. All ConstructionPrototype, WallCommand, SiegePrototype (31/31, three runs), EnemyComposition, Flyer and Machicolation tests pass.
  - Remaining failures are 15 Game-scene tests that hard-code 3m Game cells or heights: `StackedConstructionPlayTests` (8), `GameBattlementPlayTests` (5), `GameBattlePlayTests` (1), `GameFlowPlayTests` (1). The GameFlow test passes its NavMesh checks (courtyard to wall top reachable, outside unreachable) and fails only on 3m cell bounds. These are left to `QA-castle-kit-tests`.
  - Pre-existing on `develop` without SIEGE-18 (verified by stashing the changes): `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores` (likely UI-07 stored soldiers).
  - `GameOrganizationPlayTests` "close with O" failed once in a full run and passed when run alone (flaky).
- **In-editor check (agent, 2026-11-23)**: played `Game.unity`: 52 pieces placed, wall colliders 7.5 × 5 × 7.5m, gate plus fixed wall (two-storey gatehouse), stairs 5m wide; overview and courtyard renders matched the planned layout. Tab does not open the build view in the Game (it never did; the construction building's F interaction does).
- **Manual Verification (user confirmed, 2026-11-23)**: the user played the Game from the Title scene on the 7.5m grid and approved the result. Steps to repeat:
  - Open `Assets/_Project/Scenes/Title.unity`, Play, New Game. The prep camera shows the 13-cell ring.
  - Walk from the courtyard up a north stair onto the 5m wall.
  - The gatehouse is two storeys.
  - Construction view (walk to the courtyard `Construction` building, press F): a fourth wall storey on a wall column shows `이 조각은 더 높이 쌓을 수 없습니다`.
  - In battle, ladders appear at 4 north-wall lanes.
  - Continue is unavailable for old (version 1) run saves.
- **Known limits / follow-ups**:
  - Placeholder wall art is stretched to 7.5 × 5 × 7.5m; `SIEGE-19` replaces it.
  - `CastleBattlements` boxes still sit on 7.5m walls (`SIEGE-20`).
  - Wall-top archers in the walkway centre cannot hit enemies within about 15m of a 5m wall's foot; crenel stand points are in `SIEGE-20`.
  - Gate placeholder prefab and machicolation sizes: `SIEGE-22`. Ladder timing: `SIEGE-21`.
  - Game test fixes: `QA-castle-kit-tests`.
