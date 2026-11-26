# COMBAT-15 | 2026-11-26 | Combat - Walkway Width & Formations on 7.5m Walls

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

- **Assignee**: `SeongminHwang` (the task was reassigned from `youngho` at the user's request) | **Branch**: `feature/combat-15` | **Claim commit**: `9d0912b`
- **Source**: [SIEGE-14](../SIEGE/SIEGE-14.md) breakage list item 9. The task was written for a 5m grid; the Game moved to 7.5m cells instead ([SIEGE-18](../SIEGE/SIEGE-18.md), [SIEGE-36](../SIEGE/SIEGE-36.md)), so the work was rechecked at 7.5m.

## Decisions (user, 2026-11-26)

Recorded in [Combat](../../design/Combat.md) (Formations & drag placement) and [Decisions-Resolved](../../plan/Decisions-Resolved.md).

- The cap on ranks keeps following the measured walkway width. There is no separate rank limit on wall tops. Other options offered: a fixed limit of 2 ranks, or using only the front half of the walkway.
- Ranged squads follow the same rule. At Lv2 their front rank stands in the crenels.

## Findings (agent, Play Mode in `Game.unity`, 2026-11-26)

- One-cell ring wall:
  - `CastleBattlements.WalkwayWidthAt` gives 7.10m.
  - On the NavMesh, a front line placed 1.4m inside the outer face has 0.40m of room forward and 5.43m back.
  - The formation therefore gets about 5.8m of depth.
- Default Line squad (2 files):
  - 6 soldiers stand 2 × 3.
  - 12 soldiers stand 2 × 6.
  - Loose squads of 12 stand 4 × 3.
  - No two slots overlap, and no slot snaps back to the anchor.
- Lv2 (crenellated):
  - 7 crenel stand points lie within 6m of the anchor.
  - A ranged squad's front rank uses them. The rest stand behind.
  - A Loose ranged squad's front rank gets the 1.5m crenel spacing instead of the 2.5m formation spacing. This is the same as on the old 3m grid.
- 2×2 two-storey corner towers ([SIEGE-36](../SIEGE/SIEGE-36.md), stacked wall pieces):
  - The roof walkway is 14.6m both ways.
  - Line and Loose squads of 12 and 24 stand on the roof with no overlap and no slot off the roof level.
  - There are no `Tower` pieces in the Game layout yet ([SIEGE-24](../../backlog/SIEGE-24.md)).
- Crenel slots on a 7.5m edge:
  - Five crenels at 0.75 to 6.75m.
  - Both edge ends are half merlons.
  - The crenel spacing stays 1.5m across cell joints.

## Implementation

- No runtime code changed. `BattlementRules.WalkwayWidth`, `CrenelCenters`, `FormationLayout` and `SquadCommandCenter.PlanSlots` already scale with the cell size.
- New EditMode tests:
  - `BattlementRulesTests.WalkwayWidth_OnGameCells_IsTheCellMinusItsBattlements`
  - `BattlementRulesTests.CrenelCenters_OnGameCells_FiveCrenelsAtEvenSpacing_ContinuingIntoTheNextCell`
  - `FormationLayoutTests.GameWallTop_RanksFollowTheWiderWalkway`
- Docs:
  - [Combat](../../design/Combat.md): new bullet on wall tops on the 7.5m grid.
  - [WallsAndSiege](../../design/WallsAndSiege.md): battlement sizes now give the walkway as 7.5 → 7.1m and describe the 7.5m crenel pattern.
  - [Decisions-Resolved](../../plan/Decisions-Resolved.md): new entry for this decision.

## Automated Verification

- Code rules check passed: 0 violations, no serialized asset changes.
- `BattlementRulesTests` 18/18 and `FormationLayoutTests` 14/14 passed.
- Full suite (`Tools/Agent/unity-verify-local.ps1 -Mode all`):
  - EditMode `950/950` passed.
  - PlayMode `370/380`. The 10 failures do not touch the code this task covers.
- Known PlayMode failures (3):
  - `StackedConstructionPlayTests.OneCellGroundStairs_PlayerAndUnitAscendAndDescend` (2 cases).
  - Flaky `GameOrganizationPlayTests` close with O.
- Other PlayMode failures (7). They were not rerun on `develop`:
  - Camera eye-height offset of 0.22m:
    - `CombatPrototypePlayTests.FirstPersonCamera_ClampsPitch_AndFollowsPlayerAtEyeHeight`
    - `WallCommandPlayTests.Views_SwitchCameraInputAndBodyVisibility_WithoutClearingOrders`
  - HUD layout and text checks:
    - `GameBattlePlayTests` HUD overlap
    - `GameEnemyIntelPlayTests` enemy list overlap
    - `PrepHudPlayTests` (2)
    - `GameFacilityPlayTests` shop lock text

## Manual Verification

Pending at completion. The user chose to finish first.

1. Open `Assets/_Project/Scenes/Title.unity`, press Play, then New Game.
2. In preparation, open the `P` placement screen and click a wall top to move a squad there.
   - Expected: the squad stands 2 wide and several ranks deep behind the parapet.
   - No soldier stands off the walkway or on the battlement.
3. Left-drag a long line along the wall.
   - Expected: one or two ranks spread along the parapet.
4. Do the same on a corner tower roof (10m).
   - Expected: the squad stands evenly on the roof.
5. To reset, stop Play.

## Follow-ups

- [SIEGE-20](../../backlog/SIEGE-20.md) (kit battlements, cover heights and crenel positions) may move the crenel stand points. Recheck ranged front ranks on wall tops after it lands.
- Tower roofs from real `Tower` pieces arrive with [SIEGE-24](../../backlog/SIEGE-24.md).
