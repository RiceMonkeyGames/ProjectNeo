# SIEGE-19 | 2026-11-23 | SIEGE - Castle Kit Dressing in the Game Scene

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-19 - Castle Kit Dressing in the Game Scene

- **Assignee**: `youngho` | **Branch**: `feature/siege-19` | **Claim commit**: `7dadf0d`
- **Decisions (user, 2026-11-23)**; recorded in [WallsAndSiege](../../design/WallsAndSiege.md) (castle kit section) and [Decisions-Resolved](../../plan/Decisions-Resolved.md):
  - Game battlements: the kit is visual only. `CastleBattlements` keeps collision, cover, ladder sill and crenel stand points. Known mismatch until `SIEGE-20`: kit crenel sill 0.76m and merlon 1.38m against the rule values 1.1m and 2.2m; the Lv1 parapet also shows kit crenels.
  - Gate columns raised to 3 storeys get a stair room like wall columns, from the lowest storey that meets a walkway. A first version kept the storey above the gate solid for the raised portcullis; the user pointed out the portcullis hides inside the wall anyway, and measurement confirmed it (portcullis z −0.09~0.10m inside the 0.5m outer panel; raised it spans 4.35~9.57m inside the 5~10m storey), so that limit was dropped.
  - Convex outer corners: a small corbelled corner turret (visual only).
- **Scope**: kit dressing of the Game castle, convex corner turrets, gate-column stair rooms, draw-cost measurement, dev keys for testing, and in-place update of the spike piece definitions, and the portcullis phase link. Kit gate sizes stay in `SIEGE-22`; battlement rules on kit shape stay in `SIEGE-20`.
- **Implementation**:
  - Runtime `Walls/CastleKitDresser`: serialized `battlementColliders` (default true) with `BattlementColliders` property and `Configure(world, kit, withBattlementColliders = true)`. When false, `Battlement`, `BattlementCap` and `BattlementHalf` kit pieces get no collider and no `ShotCover` (visual only). Serialized `staticBatching` (default true): in Play Mode each dressed cell root is combined with `StaticBatchingUtility.Combine`, except the gate cell (its portcullis and doors animate); the combined mesh is destroyed with its cell. New `BuildBeforeNavigation(world)`: `ConstructionWorld.Awake` dresses the castle before the first NavMesh bake (like `CastleBattlements`), so the kit colliders do not trigger a second bake that blocks `GameBattleController.CanStart` right after the scene loads (found by the full PlayMode run: 19 battle-start failures, fixed).
  - Runtime `Walls/CastleBattlements`: serialized `showVisuals` (default true) with `ShowVisuals` property. When false only colliders and allied `ShotCover` are built. Included in the signature, so toggling rebuilds.
  - Runtime `Walls/StairRoomRules`: gate columns (level 0 `Gate`, all levels above `Wall`, 3+ storeys) are room columns like wall columns (private `IsRoomColumn` helper used by `Find` and `IsTallWall`). Play Mode check: gate tower raised to 3 beside one-storey walls gets a room from level 1 (doors east and west), no outside stairs, NavMesh paths from both 5m walkways and the courtyard to the 15m top complete; the raised portcullis is not visible inside the room.
  - Runtime `Walls/CastleKitRules`, `CastleKitPiece.CornerTurret = 33`, `CastleKitSettings` PieceCount 34: for the top Wall placement of a cell, every pair of adjacent faces whose neighbour cells are both outside (`BattlementRules.IsOutside`) and empty at that level gets one turret at the shared vertex (`SM_Bld_Castle_Wall_Tower_M_01`, pivot `top - CornerTurretDrop` (2.5m)). Raised corners keep one turret at the new top. Visual only.
  - Runtime `Flow/GameDevKeys` (new; active only in Editor and development builds via `Debug.isDebugBuild`): `F9` adds 1000 gold to the run wallet; `F10` (Preparation only) finishes all pending construction and repair work orders at once through the new public `GameConstructionWork.FinishAllForTesting()`. Does not spend prep hours.
  - Editor `GameCastleKitSetup` (new). Menu `ProjectNeo/Setup/Apply Castle Kit To Game Scene (SIEGE-19)` creates or updates in place `Data/GameFlow/Build/CastleKit.asset` (shared prefabs in `Prefabs/CastleKit`; room landing 2m; flight width 2.16m, the 7.5m spike values), adds `CastleKitDresser` (battlement colliders off) to the Game `ConstructionWorld`, and sets `CastleBattlements.ShowVisuals = false`. Missing kit prefabs are regenerated through `SyntyGridSpikeSetup.SaveKitPrefabs`. Menu `ProjectNeo/Setup/Add Dev Keys To Game Scene (SIEGE-19)` adds `GameDevKeys` to the Game `ConstructionWorld`. Both menus were run; `Scenes/Game.unity` saved; new prefab `Prefabs/CastleKit/CornerTurret.prefab`.
  - Editor `SyntyGridSpikeSetup`: `Replace` updates an existing asset of the same type in place (`EditorUtility.CopySerialized`, keeps name and GUID) instead of delete and recreate, so rerunning the spike menus no longer rewrites `RunContent.asset` with new GUIDs. New `internal static SaveKitPrefabs(CastleKitSettings)` shared with the Game menu. Kit table adds `CornerTurret` (Body.None).
  - Tests:
    - EditMode `StairRoomRulesTests`: `GateColumn_BesideTwoStoreyWalkways_RoomsFromTheWalkwayUp`, `GateColumn_BesideOneStoreyWalkways_RoomsFromTheStoreyAboveTheGate`.
    - EditMode `CastleKitRulesTests`: `RingCorners_GetOneTurretEach_AtTheTopOfTheCorner`.
- **Automated Verification**:
  - Filtered EditMode runs: `CastleKit` 35/35 PASSED; `StairRoom` 19/19 PASSED.
  - Code rules check passed (0 violations; serialized changes `Game.unity`, `CastleKit.asset`, `CornerTurret.prefab`, all through the setup menus).
  - Full suite (after the `BuildBeforeNavigation` fix): EditMode `839/839` PASSED. PlayMode `320/339`; all 19 failures are the known ones listed in [SIEGE-18](SIEGE-18.md): `StackedConstructionPlayTests` (8), `GameBattlementPlayTests` (5), `GameBattlePlayTests` gate-storey test (1), `GameFlowPlayTests` 3m cell bounds (1), `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`, flaky `GameOrganizationPlayTests` close-with-O. `GameMoralePlayTests` contagion passed this run.
  - Final full suite before commit (after the gate-column and portcullis changes): EditMode `839/839` PASSED; PlayMode `319/339`, the same 19 known failures plus `GameStageLoopPlayTests` hitting its 180s timeout. That test is frame-bound (fixed 1/30s steps, up to 4000 frames per stage); it timed out alone too, also with the portcullis link reverted, and passed in 77.2s once the unfocused Editor's auto-tick throttle was lifted (`editor set_auto_tick --interval-ms 0`). Earlier full runs passed it in 53–66s. Environment throttling, not a code regression.
  - In-editor draw cost (agent, Play Mode, overview camera, CPU time per manual camera render, median of 4 × 100 frames):
    - Kit off 1.05ms, kit on 1.29ms (before batching).
    - With per-cell static batching, kit on 1.26ms against kit off 1.31ms (within noise).
    - 391 of 419 kit renderers are static-batched (gate cell excluded).
    - Combine cost about 7ms for all 47 cells, paid once at first dressing.
  - In-editor dressing (agent): Game kit dressing covers 48 cells with about 409 placements (about 8.5 pieces per cell), 419 renderers, 2 materials.
- **In-editor checks (agent, 2026-11-23)**:
  - Collapses in the Game: a wall breach gives a V ruin (BrokenTop 4, RubbleTop 2, pile 1); a corner collapse removes that corner turret; a bastion collapse leaves only a rubble pile; rebuilding a breached cell forgets the breach and re-dresses the cell.
  - A breach directly behind a courtyard stair stays blocked by that stair (NavMesh `PathPartial`). This is the existing design; enemies must break stairs (stair targeting is `SIEGE-26`).
  - Gate tower with 2-storey neighbours and a 3rd storey: stair room at level 2; NavMesh paths from both 2-storey walkways and from the courtyard to the gate top complete.
  - Pieces under construction keep the placeholder and get no stair room until finished (expected; `F10` helps testing).
  - Portcullis phase link (user request after first review): `Flow/GamePrepGate` sets `CastleKitDresser.PortcullisRaised = phase != Battle` on start and on every phase change (user decision 2026-11-22: raised in preparation, lowered at battle start). Play Mode check: raised in preparation; after `TryStartBattle` it lowers to the ground within `gateMotionSeconds` (1.5s); a broken gate still hides it. `GameBattlePlayTests` shows only its known gate-storey failure.
- **Manual Verification (for the user)**:
  - Open `Assets/_Project/Scenes/Title.unity`, press Play, then `New Game`.
  - Expected: the castle walls show the kit (stone panels, battlement shapes); each convex outer corner shows a small corbelled turret.
  - `F9` adds 1000 gold; the top HUD gold rises by 1000.
  - Walk to the courtyard `Construction` building and press `F` to open the build view. Raise one wall cell to 3 storeys, pressing `F10` after each storey so the work finishes. Expected: after the third storey, a stair room with doors appears in that cell; no outside wooden stairs.
  - Raise the gate cell to 3 storeys (press `F10`). Expected: a stair room in the gate tower with doors to the one-storey walkways on both sides, no outside wooden stairs, and no portcullis visible inside the room. Raising both gate neighbours to 2 storeys first moves the room floor up to their walkway.
  - Look at the gate from outside, then press `전투 시작`. Expected: the portcullis is raised in preparation and slides down over about 1.5 seconds when the battle starts.
  - Reset: stop Play Mode; the run and the castle return to the Title-scene state on the next Play.
- **Known limits / follow-ups**:
  - Kit battlement shape versus rule values (sill and merlon heights, Lv1 parapet crenels): `SIEGE-20`.
  - Kit gate and machicolation sizes: `SIEGE-22`.
  - Enemy stair targeting (breach behind a courtyard stair): `SIEGE-26`.
  - Game test fixes for castle kit and 7.5m geometry: `QA-castle-kit-tests`.
