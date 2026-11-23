# QA-castle-kit-tests | 2026-11-24 | QA - Game Tests on the 7.5m Castle Grid

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

- **Assignee**: `youngho` | **Branch**: `feature/qa-castle-kit-tests` | **Claim commit**: `79512b1`
- **Dependency**: [SIEGE-18](../SIEGE/SIEGE-18.md) (Game on 7.5m cells with 5m storeys) left 15 Game PlayMode tests failing on 3m cell coordinates and heights. Source: [SIEGE-14](../SIEGE/SIEGE-14.md) breakage list item 13.
- **Scope**: test files only. No runtime code, scenes or serialized assets changed.

## Changes

Default Game castle used by the tests (`Editor/GameFlowSetup`): 7.5m cells, ring x 10-22 and z 11-23, gate (16, 23) with a fixed wall on top (two-storey gatehouse), ladder lanes x 12, 14, 18, 20, grid `forbidValleys` and `groundStairsOnly` on.

- `StackedConstructionPlayTests` (9 tests):
  - `OneCellStairs_PlayerAndUnitAscendAndDescendTwoTiers` is renamed `OneCellGroundStairs_PlayerAndUnitAscendAndDescend` (4 rotations). Stairs go on the ground only on the Game grid (SIEGE-27), so the test now places one ground stair and one wall in the open field at (5, 31). It checks that the stair height equals one wall storey, that placing stairs on the wall returns `StairsGroundOnly`, the unit and player climbs, and that removing the stair cuts the path.
  - The two-tier stair test is replaced by `성벽을_2층과_3층으로_올리면_안뜰에서_자동_계단으로_맨_위까지_길이_이어진다`. It raises the west wall cell (10, 17) to two and then three storeys and checks a complete NavMesh path from the courtyard to each top: the automatic outside stairs (1↔2) and the stair room (to 3). A fourth storey returns `StoreyCap`. This is the first Game-scene PlayMode coverage of these stairs.
  - The gate test is renamed `성문_위_성벽은_성문을_부숴도_아치와_함께_유지되고_아래_통로가_열린다`. It uses the existing fixed gatehouse wall (`IsFixed`, level 1) instead of placing one. The preview-on-gate assertions are dropped because the default gate is already covered.
  - The bastion test is renamed `아래_보루가_부서지면_위_보루와_그_위의_무기가_함께_무너진다`. On the Game grid the valley rule (SIEGE-14, `ConstructionWorld.Collapse`) destroys every storey above a destroyed piece, so the upper bastion and its cannon are removed rather than dropped one storey. The test checks that the column is empty, the neighbouring two-storey wall stays, and the cell can be rebuilt. The drop-one-storey behaviour remains only on grids without `forbidValleys`.
  - Cells moved to the new layout: stacked dismantle at the north-west corner (10, 23); bastion beside the north wall (11, 23).
- `GameBattlementPlayTests` (7 tests): `MerlonsPerFace = 6` (a 7.5m face with a 1.5m merlon cycle and a crenel in the middle); bastion faces 18 merlons, 12 with a gun port. `BuiltCount` must equal the 48 ring cells (was "greater than 100"). In the archer test the defender stands 1m behind the parapet instead of the walkway centre, because the centre of a 7.5m walkway is not visible from the ground near a 5m wall (known limit in SIEGE-18; crenel stand points are `SIEGE-20`).
- `GameBattlePlayTests`: the gate test uses the existing gatehouse wall as the left upper wall and places only the right one.
- `GameFlowPlayTests`: player start bounds use the new ring (x 11-22, z 12-23 cells); ring span 97.5m. The building entrance probe moved from 2m to 1m in front of each building's south face, because the Training building stands 2.5m from the south wall's inner face and the 2m point falls inside the NavMesh edge margin.

## Automated Verification

- Code rules check: passed (0 violations; the one serialized asset change is the unrelated TextMesh Pro fallback asset, not committed).
- Compile passed. EditMode `866/866` PASSED.
- PlayMode `351/354`, 207s (the `unity-verify.ps1` 180s poll limit reported `TIMEOUT`; the final result was read with `unity command test status`). All 4 changed classes pass: `StackedConstructionPlayTests` 9, `GameBattlementPlayTests` 7, `GameBattlePlayTests` 15, `GameFlowPlayTests` 15.
- Remaining failures are pre-existing and listed in [SIEGE-18](../SIEGE/SIEGE-18.md): `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`.
- `GameFlowPlayTests.BattleHold_CancelsOnReleaseAndStartsOnceAtCurrentWallPose` (not changed) was intermittent in isolated runs (3 of 7 failed, with different failures: phase stayed Preparation, or camera rotated 58°). It depends on real input, cursor lock and Editor focus. It passed in the full run.
- Manual play and standalone build: not run.

## Manual Verification Guide

Open `Assets/_Project/Scenes/Game.unity` and enter Play Mode.

1. The gate is a two-storey gatehouse.
2. In the construction view, stairs cannot be placed on a wall.
3. Raise the west wall middle cell to two and then three storeys. From the courtyard, walk up: storey 2 by the outside automatic stairs, storey 3 by the stair room. A fourth storey is refused.
4. Build a two-storey bastion in front of the north wall and mount a cannon on top. Destroy the lower bastion: the upper bastion and the cannon disappear together and the cell can be rebuilt.
5. Exit Play Mode to reset.

## Follow-ups

- `Tools/Agent/unity-verify.ps1` stops polling PlayMode after 180s; the full suite now takes about 207s.
