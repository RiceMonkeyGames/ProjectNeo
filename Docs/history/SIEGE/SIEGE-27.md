# SIEGE-27 | 2026-11-23 | SIEGE - Interior Stair Rooms for Steps to Three-Storey Walls

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-27 - Interior Stair Rooms for Steps to Three-Storey Walls

- **Assignee**: `youngho` | **Branch**: `feature/siege-27` | **Claim commit**: `3079744`
- **Origin**: registered from [Q-59](../Q/Q-59.md). Walls reach 3 storeys, but the automatic outside stairs only joined walls one storey apart. The user then decided that every step up to a three-storey wall (1↔3, 2↔3) uses an interior room, and only 1↔2 keeps the outside stairs ([Decisions-Resolved](../../plan/Decisions-Resolved.md)). Rules: [WallsAndSiege](../../design/WallsAndSiege.md).
- **Scope** (spike, `SyntyGridSpike` scene only):
  - Interior stair rooms for steps to three-storey walls.
  - Outside automatic stairs only for 1↔2 steps.
  - The player-built stair piece is ground-only on the 5m grid.
- **Implementation**:
  - `Walls/StairRoomRules.cs` (pure): finds stair-room cells (in each group of joined 3+ storey `Wall` columns, the cells beside a 1- or 2-storey walkway at a row end, else the first touching cell), their base and top levels, foot and first-flight faces and per-storey walkway doors. Other cells stay solid. `IsTallWall` marks columns that never get outside stairs.
  - `Walls/StairRoom.cs` (readonly struct): room result with `Doors(level)` / `HasDoor`.
  - `Walls/StairWell.cs` (pure, reusable for towers in `SIEGE-29`): one-cell switchback per storey (flights hug the side walls, `flightWidth`, floored middle corridor) with landing, hole, ceiling areas and link points.
  - `Walls/CastleKitRules.cs`: `Collect` takes optional `fallenTops` and `rooms`; `AddRoom` dresses room storeys (`RoomStairs`, `RoomCollider`, scaled `Floor` with a roof opening, `RoomLink` NavMesh links, `RoomPost` beams under upper-flight posts). Outside `WallStairs` are dropped toward 3+ storey walls. `AddSideEntryCornerStair` dresses a 1-storey corner entered only through a stair-room door: quarter-turn stair without side boards (`CornerStairsOpen` + `RoomCollider` ramps and landing) and a `RoomLink` from the door floor.
  - `Walls/CastleKitPiece.cs`: `RoomStairs=27`, `RoomCollider=28`, `RoomLink=29`, `RoomPost=30`, `CornerStairsOpen=31`. `CastleKitPlacement` gained `StretchX`, `StretchZ`, `Tilt`.
  - `Walls/CastleKitSettings.cs`: `RoomWallInset` 0.31, `RoomLandingDepth` 1, `RoomSlabThickness` 0.2, `RoomRampThickness` 0.15, `RoomHoleOverhang` 0.6, `RoomFlightWidth` 1.46, stair post positions, `RoomPostHeight` 2.5, `RoomPostThickness` 0.95 (module metres).
  - `Walls/CastleKitDresser.cs`: remembers `fallenTops` from `PieceCollapsing`, exposes `Rooms`, `UpdateStairRoom` disables the wall box collider on room storeys and adds a `HollowTop` roof slab so build mode can still pick the top.
  - `Walls/BuildGridDefinition.cs`: `GroundStairsOnly` (`Create(..., groundStairsOnly)`). `Walls/BuildLayout.cs`: `Check` returns `PlacementIssue.StairsGroundOnly = 15`.
  - `UI/ConstructionHud.cs`: text for the ground-only stair rule (spike only; Game `SiegeHud` waits for `SIEGE-18`).
  - `Editor/SyntyGridSpikeSetup.cs`: grid created with `groundStairsOnly`, kit adds `RoomStairs`, `RoomPost` and `CornerStairsOpen`, seeds raise the south wall row (x 3–12) to storeys 1 3 1 1 3 2 2 3 2 1.
- **Design note**: stair layout, solid walls with stair rooms only at row ends, the side-entry corner stair, NavMesh links and sizes are recorded in [WallsAndSiege](../../design/WallsAndSiege.md). Revised after the user's first spike review (2026-11-23): upper-flight posts reached the floor only to the landing (now continued by wooden posts), flights narrowed to about 2/3, and joined 3-storey walls got stairs only at the ends. After reviewing real curtain-wall construction the user chose the compromise (2026-11-23): walls stay solid and only stair cells are rooms, replacing the open-hall version tried the same day.
- **Automated Verification**:
  - Code rules check passed.
  - EditMode `824/824` PASSED (includes `StairRoomRulesTests`).
  - PlayMode (final run after the third revision): `333` passed, one failure, `GameMoralePlayTests.ContagionTicksInScene_OnlyNearbyAlliesLoseMorale_AndHudShowsCheck` (expected 145, was 150). It also fails with the SIEGE-27 changes stashed, so it is pre-existing and unrelated (same failure as in [SIEGE-16](SIEGE-16.md)).
- **In-editor Play Mode checks** (`SyntyGridSpike`, not a full PlayMode suite):
  - NavMesh paths complete both ways through all three seeded rooms (x=4, x=7, x=10).
  - An allied unit walked low walkway → roof and back, and 1→2 / 2→1 storeys through the rooms.
  - A simulated player `CharacterController` climbed room x=7 from the 1-storey door to the 2-storey door and back, and room x=10 to the roof.
  - Collapse of a 1-storey neighbour kept the door as an opening; collapse of the room column removed the room.
  - After the revision: four 3-storey walls built at runtime beside the east wall (x=11, z 5–8) gave stairs only in (11,5) and (11,8) with open halls between; NavMesh paths, an allied unit and a simulated player crossed the hall at 5m and 10m and climbed through an end room to the roof.
  - Second revision (user report: a four-cell 3-storey row stayed closed because its middle cells touched no lower walkway): joined 3-storey groups now become rooms as a whole from the group's lowest room floor (`StairRoomRules.JoinColumns`). Raising the north row x=8–11 to 3 storeys in Play Mode gave open halls in (9,10) and (10,10), stairs in the end cells, and complete paths along the hall at 5m and 10m and to the roof from the gate top and the corner wall. EditMode `821/821`.
  - Third revision (user decision: solid walls, stair rooms only at row ends; user report: a 2-storey, 1-storey corner, 3-storey corner got a straight stair that could not be boarded from the room door): with the north-east row x=11, z 5–8 at 3 storeys, only (11,5) and (11,8) became stair rooms and paths ran along the roof walkway and from the east walkway to the roof. With (3,4) raised to 2 storeys, corner (3,3) got the side-boarded quarter-turn stair; the path from (3,4) down to the corner took 7 corners (it went around the castle before), and a simulated player went from the room door onto the stair side, up to the 2-storey top and back into the room. EditMode `824/824`.
- **Manual Verification**: the user played the spike after each revision (first rooms, posts and flight width, joined walls, the 2-storey / 1-storey corner / 3-storey corner) and reported the issues fixed above; the final revision (solid walls with end stair rooms, side-entry corner stair) was checked in the editor as listed and the user asked to finish. Steps to repeat:
  - Open `Assets/_Project/Scenes/SyntyGridSpike.unity` (menu `ProjectNeo/Setup/Create Synty Grid Spike (SIEGE-14)` rebuilds it), then Play or enter the build view.
  - Rooms at the south wall row, x 4, 7 and 10: climb each from the low walkway to the roof and back; every walkway door at the same floor height should be open; posts under the upper flights reach the floor.
  - Outside stairs should appear only between 1- and 2-storey walls (x 11→12).
  - Build four 3-storey walls side by side next to a 1-storey wall: only the two end cells become stair rooms; the middle stays solid and the row joins along the roof walkway.
  - Build a 2-storey wall, 1-storey corner and 3-storey wall corner: the corner gets a quarter-turn stair that is boarded from its side next to the room door.
  - Try placing a stair piece on a wall top; it should be refused with the ground-only message.
  - Reset: stop Play Mode; do not save the scene.
- **Known limits**: enemy target choice for these rooms is `SIEGE-26`. Game migration (grid flag, `SiegeHud` text) waits for `SIEGE-18`.
- **Follow-ups**: `SIEGE-18` (Game migration incl. `SiegeHud` text and Game grid flag), `SIEGE-26` (enemy targeting), `SIEGE-29` (towers reuse `StairWell`).
