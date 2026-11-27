# SIEGE-24 | 2026-11-27 | SIEGE - Towers in the Game

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-27: SIEGE-24 - Towers in the Game

- **Assignee**: `youngho` | **Branch**: `feature/siege-24` | **Claim commit**: `792f376`
- **Origin**: registered 2026-11-23 from the move of the Game to the 5m castle kit ([Decisions-Resolved](../../plan/Decisions-Resolved.md)); source [SIEGE-14](SIEGE-14.md) breakage list item 17. Rescoped 2026-11-26 to the 7.5m cells with 5m storeys of [SIEGE-35](SIEGE-35.md) and [SIEGE-18](SIEGE-18.md). Rules: Towers in [WallsAndSiege](../../design/WallsAndSiege.md).
- **Decisions (user, 2026-11-27; recorded in [Decisions-Resolved](../../plan/Decisions-Resolved.md))**:
  - Default run-start corner towers (SIEGE-36) stay plain wall cells, not `Tower` pieces. They get no occupant buff, roof mounts, storey rooms or tower collapse look.
  - The `Tower` piece sits **in the wall line**. Two of its cells replace two wall-line cells automatically when placed (no manual dismantle), and the other two stick out outward.
  - Cost is 60 gold **per tier**. Construction work goes tier by tier. Dismantling (revised after the user's manual check the same day) starts at the top: tiers above the initial 2 come off one at a time, and the initial 2 tiers come off together.
  - The automatic height rule (one storey above the tallest touching wall) is removed. A new tower always gets 2 tiers. More tiers are stacked by hand up to the grid limit of 4 storeys.
  - The walkway stays continuous. A higher tower keeps the SIEGE-29 doors. A roof level with the walkway has no battlement on that face. A roof lower than 3-storey walls gets a stair room in the touching wall cell, with the tower roof counted as the lower walkway.
- **Implementation**:
  - Definition (`Walls/BuildPieceDefinition`): new `initialTiers` (`InitialTiers`, `SetInitialTiers`).
  - Rules (`Walls/TowerRules`): `Check` now covers stacking only. A new tower at level 0 gives `TowerNeedsWall` and is placed by the new `CheckInLine`: the inner row holds only unfixed wall-only columns, the outer row is empty outside cells, the cell behind the inner row is not outside, ladder lanes give `LadderLane`, and a ground stair leaning on the inner row gives `Occupied`. `IsStackOnTower` and `Tiers(grid, definition, level)` replace the old touching-wall height rule. The replaced list is filled top storey first.
  - Stair rooms (`Walls/StairRoomRules.Walkway`): towers count as walkways.
  - World (`Walls/ConstructionWorld`): `CheckPlacement` routes new towers to `CheckInLine`. `RefreshOutside`, `IsNewTower`, `PlacementCount`, `CollectReplaced`, `SnapAnchor` (the 2×2 preview snaps to the nearest valid spot, or to the tower under the pointer) and `ReplaceWalls` are new. `PieceReplacing` fires before each replaced wall storey is removed. `TryPlace(..., List<PlacedStructure>)` returns every stacked piece.
  - Placement (`Walls/PlacementController`): `SnapAnchor` in `UpdatePreview`, `PreviewCount`, the ghost is scaled to the tier count, and `PiecePlaced` fires for every placed piece. Valley fills now charge and create a work order per piece (previously once per fill).
  - Economy (`Run/BuildEconomy.CanAfford(piece, count)`, `Flow/GameBuildEconomy`): count-aware `CanConfirm`. `OnPieceReplacing` refunds each replaced wall storey like a dismantle, cancels its work order, and shows `성벽 N층을 망루로 바꿈 - X골드 환불`.
  - Work (`Flow/GameConstructionWork.FinishForBattle`): build orders are sorted top level first. A 0% piece that is covered stays at 5% health.
  - Morale (`Morale/GameSquadMorale`): a tower collapse counts as wall loss.
  - Collapse look (`Walls/CastleKitDresser.RememberBreach`): tower cells on the wall line (`OnWallLine`) become V breaches like walls. Outward cells keep rubble and stump.
  - HUD (`UI/ConstructionHud`): `TowerNeedsWall` text and an `N층 한 번에` hint.
  - Setup (`Editor/GameTowerSetup`, menu `ProjectNeo/Setup/Apply Towers To Game (SIEGE-24)`): `Data/ConstructionPrototype/Tower.asset` (2×2, 5m storey, HP 400, 2 initial tiers, roof mounts `roof_sw/se/nw/ne` at ±0.13, occupant +20% attack range and attack), `Prefabs/ConstructionPrototype/Tower.prefab`, `BuildCosts` 60 gold, `RunSaveSetup`, and the Game scene `ConstructionHud.tower` and `TowerOccupantBuff` on the `ConstructionWorld`.
- **Tests**:
  - EditMode `TowerRulesTests` rewritten and extended: in-line placement, courtyard and away rejects, ladder lane and stair, fixed gate, `IsStackOnTower`, `Tiers`, a stair room beside a lower roof, and a roof level with the walkway needing no room.
  - EditMode `StairRoomRulesTests`: renamed `NoRoom_ForOneTwoSteps_UnfinishedStoreys_OrATowerAsTallAsTheWall`; added `LowerTowerRoofBesideAThreeStoreyWall_GetsADoorAtTheRoofHeight`.
  - PlayMode `GameTowerPlayTests` (new, 4 tests): 2-tier in-line placement replacing two wall cells with per-storey cost; no placement inside the wall, on open ground or without gold; roof buff with run-save round trip per storey; collapsed tower leaves a gap in the wall line and can be rebuilt.
- **Automated verification (so far)**:
  - EditMode "Rules" filter: 445/445 passed.
  - PlayMode `GameTowerPlayTests`: 4/4 passed.
  - Full suite (2026-11-27, `unity-verify.ps1 -Mode all`, PlayMode result read from `unity command test status` after the wrapper's wait ran out): code rules passed (0 violations); EditMode 1000/1000 passed; PlayMode 385/394 passed. The 9 failures are the pre-existing ones recorded in [SIEGE-30](SIEGE-30.md) and [AUDIO-01](../AUDIO/AUDIO-01.md) (eye height, one-cell ground stairs, HUD layout or text checks); the 4 new `GameTowerPlayTests` pass.
  - Review fixes after the full run: the siege prototype's `Siege/SiegePrepController` now checks the budget for every piece of a multi-piece placement (`PreviewCount`) and refunds walls a tower replaces (`PieceReplacing`); `TowerRules.CheckInLine` reuses its row list (no per-frame allocation from the preview snap) and returns `TowerNeedsWall` when no outside set is given. Rule tests and `GameTowerPlayTests` were rerun after these fixes.
  - Fix from the user's manual check (2026-11-27): a tower room storey beside a taller wall at the same storey (2-tier tower between a one-storey and a two-storey wall) showed an empty face, because the tower treated that face as covered by the wall and the wall treated it as covered by the tower. `CastleKitRules.AddTower` now places an inner `TowerPanel` on such faces (not when the neighbour is a stair room, and only once between two tower rooms), like stair rooms already did. New EditMode `CastleKitRulesTests.TowerRoomStorey_BesideATallerWall_GetsAnInnerWallOnThatFace`; EditMode "Rules" filter 446/446 passed; a Play Mode capture from inside the 5m room shows the closed face.
  - Second manual-check fix (2026-11-27): a 2-tier tower came off in two dismantles, and the red dismantle highlight was one tier of one cell, buried in the tower's kit model. `GameBuildEconomy.DismantleGroup` now picks the tiers to remove (always from the top; one tier while the tower is above its initial tiers, otherwise all initial tiers), `Dismantle` removes the group with a refund per tier (`DismantleTiers`), and the highlight (`Walls/DismantleHighlight.Show`, new `kitCells`/`stack` parameters) hides the kit model of all four cells and draws one red box over every tier in the group. `GameTowerPlayTests` updated (4/4 passed); a Play Mode capture shows the box over the whole 2-tier tower.
  - Final full suite before the commit (2026-11-27, after both manual-check fixes): EditMode 1001/1001 passed; PlayMode 385/394 passed, the same 9 pre-existing failures as above.
- **Known limits / follow-ups**:
  - Enemy targeting of towers, and whether enemies cross the 1m stump in the outward cells, belong to `SIEGE-26`.
  - On the north wall, ladder lanes every 2 cells mean a 2×2 tower cannot be placed on most of the wall.
  - The spike scene `SyntyGrid75Spike` keeps its seeded outside towers.
  - Tower HP, roof mounts, the +20% buff and the 60 gold cost are temporary values to tune after playtest.
- **Manual Verification** (Game):
  - Open `Assets/_Project/Scenes/Game.unity`, press Play, and start from `Title`.
  - Press `F9` to add 1000 gold.
  - In the construction bar, select the tower card. Hover a south, east or west wall. Expected: the 2×2 preview snaps onto the wall line, with two cells inside the wall and two outside, and the preview is two storeys tall.
  - Click to confirm. Expected: 120 gold is charged (60 per tier), the two wall cells are replaced, and the message `성벽 2층을 망루로 바꿈 - X골드 환불` appears for each replaced storey. A tower work order is queued.
  - Press `F10` to finish all pending work. Expected: the tower and its two work orders (one per tier) are finished.
  - Select the tower card again and point at the top of the finished tower. Click to add a third tier. Expected: one tier is added and charged 60 gold.
  - Raise the neighbouring wall cells to 3 storeys using the wall piece. Expected: a stair room with doors onto the tower roof appears on both sides.
  - Turn on dismantle and hover the 3-tier tower. Expected: a red box over the top tier only; clicking removes it (60 gold refunded). Hover again: the red box covers the whole 2-tier tower; clicking removes both tiers at once (120 gold refunded when built this preparation).
  - Build the tower again and aim at it and press `F11` to collapse it. Expected: the Console logs the piece, rubble and a stump sit in the outward cells, and V breaches appear at both wall ends.
  - Reset: stop Play Mode. Collapses and placements are not saved to the scene.
</content>
