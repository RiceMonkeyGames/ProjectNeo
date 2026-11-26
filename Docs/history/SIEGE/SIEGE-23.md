# SIEGE-23 | 2026-11-26 | SIEGE - Bastions on the 7.5m Grid

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-26: SIEGE-23 - Bastions on the 7.5m Grid

- **Assignee**: `youngho` | **Branch**: `feature/siege-23` | **Claim commit**: `24fa72c`
- **Dependency**: [SIEGE-18](SIEGE-18.md) (7.5m grid, 5m storeys). Breakage list item 7 of [SIEGE-14](SIEGE-14.md).
- **Rescope (user, 2026-11-26)**: the task was written for a 5m grid before [SIEGE-35](SIEGE-35.md) moved the Game to 7.5m cells with 5m storeys. The user asked to redo it on the 7.5m grid and to fix the other `youngho` backlog tasks that still described the 5m grid.
- **Already in place before this task**:
  - `Bastion.asset` is 1×1 cell, 5m per storey, up to 3 storeys (`Editor/Grid75Migration`, SIEGE-18).
  - A fallen bastion leaves only a rubble pile and the wall behind stays whole ([SIEGE-19](SIEGE-19.md)); a lower bastion's loss removes the upper tiers and their weapon ([QA-castle-kit-tests](../QA/QA-castle-kit-tests.md)).
  - Bastions refuse ladder lane cells. The mount point is stored in normalized piece space (`PlacedStructure.TryGetMountPose`), so it sits on the top centre of a 7.5m × 5m piece without changes.
- **Implementation**:
  - `Editor/BastionSetup.cs`: `Height` now equals `Grid75Migration.Storey` (5m) instead of 3m, so fresh setups create the bastion at the Game storey height before `Grid75Migration.ConfigurePieces` runs. No serialized asset changed.
  - [WallsAndSiege](../../design/WallsAndSiege.md) Bastions: piece size given as 7.5m × 7.5m × 5m per storey, up to 3 storeys; per-cell balance left to `SIEGE-25`.
  - Backlog tasks rewritten for 7.5m cells and 5m storeys: `SIEGE-20`, `SIEGE-22`, `SIEGE-24`, `SIEGE-25`, `SIEGE-26`, `SIEGE-30`, `ART-08`, `FLOW-05`. `FLOW-05` notes that old saves are dropped, not migrated (SIEGE-18 and SIEGE-36), and keeps only the open question of saving kit state. `SIEGE-28` already counts in 5m storeys and was left as is.
- **Automated Verification**:
  - Code rules check passed (0 violations, 0 serialized asset changes). Unity compile passed.
  - Full suite (2026-11-26): EditMode `950/950` PASSED. PlayMode `371/380`. The 9 failures do not involve this task: the only code change is an Editor setup constant that no test references, and no runtime script or serialized asset changed. They are not rerun on `develop` here.
    - First-person camera pitch, 0.22 left over where 0.01 is expected: `CombatPrototypePlayTests.FirstPersonCamera_ClampsPitch_AndFollowsPlayerAtEyeHeight`, `WallCommandPlayTests.Views_SwitchCameraInputAndBodyVisibility_WithoutClearingOrders`.
    - HUD layout and text: `GameBattlePlayTests` (battle HUD overlap), `GameEnemyIntelPlayTests` (enemy list overlaps `BattleStatus`), `PrepHudPlayTests` (2), `GameFacilityPlayTests` (shop lock text is now `잠금 한도: 1개`, the test expects `최대 1개`).
    - `StackedConstructionPlayTests.OneCellGroundStairs_PlayerAndUnitAscendAndDescend(0)` and `(2)`: the unit stays on the ground about 6.5m from the stair-top target.
- **In-editor Play Mode check (agent, `Game.unity`, temporary script outside `Assets/`)**:
  - North wall cell (14, 36) raised to 3 storeys; three bastions stacked on (14, 37) with `PlacementIssue.None` each. Top bastion bounds 7.5 × 6.1 × 7.5m (with battlements), cannon mounted at the top centre at 15m. The bastion top joins the 3-storey walkway at the same height.
  - Destroying the lowest bastion removed all three tiers and the cannon, left a rubble pile, kept the 3-storey wall (outer face re-dressed with battlements), and the cell could be built again.
  - Console: only a Synty Sidekick database error logged before the session (unrelated).
- **Manual Verification** (steps for the user):
  - Open `Assets/_Project/Scenes/Title.unity`, Play, New Game.
  - Walk to the courtyard `Construction` NPC and press F to open the build view.
  - Place a bastion against the outside of the north wall (not on a ladder lane), stack it, and mount a cannon on the top.
  - Expected: each bastion storey is as wide as a wall cell and 5m tall; the cannon sits on the top centre; when the lowest bastion falls, the upper tiers and the cannon go with it, rubble remains, and the wall behind stays whole.
  - Reset: stop Play Mode; do not save the scene.
- **Known limits / follow-ups**: bastion HP and cost per cell are unchanged (`SIEGE-25`). The cannon model looks small on a 7.5m top (art, not in scope). Collapse stump and wall scars: `SIEGE-30`.
