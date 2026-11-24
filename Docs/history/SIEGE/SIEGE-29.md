# SIEGE-29 | 2026-11-24 | SIEGE - Tower Storey Rooms & Doors at Every Walkway Height

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-24: SIEGE-29 - Tower Storey Rooms & Doors at Every Walkway Height

- **Assignee**: `youngho` | **Branch**: `feature/siege-29` | **Claim commit**: `77a7ca6`
- **Origin**: registered 2026-11-23 from the user's spike review: a tower had a room and door only in its top storey (`TowerRules.RoomOf`), so a tower more than one storey above a touching wall had no door from that walkway. Rules: Towers in [WallsAndSiege](../../design/WallsAndSiege.md).
- **Scope** (spike only; Game integration is `SIEGE-24`): built and checked in the 7.5m spike `Assets/_Project/Scenes/SyntyGrid75Spike.unity` (the Game uses 7.5m cells since `SIEGE-18`; the task text named the 5m spike). The 5m spike scene was not rebuilt.
- **Implementation**:
  - `Walls/TowerRules.cs`: `RoomOf` works for any tier. Every tier from the lowest touching walkway floor up is a room; lower tiers stay solid. The stair cell and door cell come from that lowest floor so all room storeys stack the stair in one cell. `Interior` gained `FirstFloor`, `Walkways` and `Doors` face bits (`MeetsWalkway`, `HasDoor`); new `FaceDoorCell` (a face's door cell, never the stair cell). `RiseFace` now points from the stair cell toward the door cell.
  - `Walls/CastleKitRules.cs`: `AddTower` dresses every room tier (walkway faces `TowerPanel`/`TowerDoor`, other faces `TowerArrowslit`, collider halves). New `AddTowerRoom` (lowest room floor, ceiling, one `WallStairs` flight per room tier, `RoomLink` NavMesh links at each flight top and at each flight foot above the lowest room storey) and `AddCeilingWithOpening` (opening only over the flight). `AddTowerTop` keeps only the roof ring. `Stair` takes an optional `back` offset; `HalfFloor` an optional length.
  - `Walls/CastleKitSettings.cs`: code properties `TowerStairInset`, `TowerOpeningDepth`, `TowerStairBack`, `TowerHoleMargin`, `TowerStairTop/HoleStart/HoleEnd(cell)` (no serialized fields added).
  - `Walls/CastleKitDresser.cs`: `UpdateTowerRoom` runs for every tier; `HollowTop` slabs leave the same opening; the room key includes door bits.
  - `Editor/SyntyGridSpikeSetup.cs`: grid level limit 4; towers of 2, 3 and 4 storeys beside one-storey walls at (1,4), (1,7), (13,4); two-storey walls at (9,13) and (10,13) so the existing (9,11) tower stands between two-storey walls. The 7.5m spike scene and its `Data/SyntyGrid75Spike` assets were regenerated with the menu.
- **Fixes found while checking (7.5m cells)**:
  1. The ceiling opening was half a cell (3.75m) while the flight is 2.16m wide, leaving a 1.3m gap beside the flight top: the opening now ends at the flight's far side.
  2. NavMesh erosion split the floor beside the opening from the flight on upper storeys: `RoomLink`s bridge them.
  3. A flight ending against the outer wall left the player's capsule (radius 0.4m, step 0.3m) stuck about 0.7m below the floor above, and the flight's side stringer blocked stepping off sideways.
  4. User review: the stair top faced a wall right after climbing. The flight now starts at the outer-wall end of the stair cell (about 2.2m of floor behind its foot) and climbs toward the door cell, ending at the cell edge on open floor; the opening covers only the flight.
- **Automated Verification**:
  - EditMode `901/901` PASSED (new: `TowerRulesTests.TallTowerBesideOneStoreyWalls_HasRoomsFromTheWalkwayUp_AndADoorOnlyAtTheWalkwayStorey`, `TowerRulesTests.ThreeStoreyTowerBetweenTwoStoreyWalls_HasRoomsOnlyFromTenMetres_WithADoorOnEachWalkwaySide`, `CastleKitRulesTests.TallTower_StacksOneInteriorStairPerRoomStorey_AndOpensEveryCeilingAboveIt`; `TowerRulesTests.TopStorey_IsARoomWithADoorOnTheWalkwaySide_AndAStairBesideIt` now expects the stair to rise toward the door cell).
  - PlayMode `365/369`. The 4 failures are the known pre-existing ones listed in [SIEGE-31](SIEGE-31.md): `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다` (passed on its own rerun), `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`.
- **In-editor Play Mode checks** (`SyntyGrid75Spike`, temporary editor probes deleted afterwards):
  - NavMesh paths complete both ways from each walkway to each roof: 2-, 3- and 4-storey towers beside one-storey walls, the 3-storey tower from both its south and north two-storey walkways, and south walkway to north walkway through that tower.
  - The player's `CharacterController` (radius 0.4, step 0.3, slope 46) driven along waypoints climbed the 3-storey tower from the 5m room to the 15m roof and the 4-storey tower from the 5m room to the 20m roof, walking off every flight top onto the floor and around to the next flight foot.
- **Known limits**: on 5m cells the flight is as long as the cell, so its foot touches the outer wall (5m spike only; the Game uses 7.5m). Kit pieces are placed one by one by rules; a switch to assembled module prefabs was raised by the user and is not decided.
- **Manual Verification**:
  - Open `Assets/_Project/Scenes/SyntyGrid75Spike.unity` (menu `ProjectNeo/Setup/Create 7.5m Grid Spike (SIEGE-35)` rebuilds it) and Play.
  - Enter the 3-storey tower west of the west wall (north of the 2-storey one) and the 4-storey tower east of the east wall through their walkway doors; walk behind each flight's foot, climb, and walk off the top onto the floor; repeat up to the roof and back down.
  - The tower north of the gate's east 2-storey row: enter from the south walkway and from the new north wall's walkway; both doors lead to the 10m room.
</content>
</invoke>
<invoke name="Bash">
<parameter name="command">cd "C:/Users/JUNGLE/Desktop/youngho/AI GAME PROJECT/ProjectNeo" && powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/doc-finish-check.ps1 -TaskId "SIEGE-29" 2>&1 | grep -E "HISTORY_EXISTS|BROKEN_LINKS|KOREAN|CODEMAP|INVALID|ACTIVE_BACKLOG"