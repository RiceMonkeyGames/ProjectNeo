# SIEGE-35 | 2026-11-23 | SIEGE - 7.5m Footprint Grid Spike (5m Storey Height)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-35 - 7.5m Footprint Grid Spike (5m Storey Height)

- **Assignee**: `youngho` | **Branch**: `feature/siege-35` | **Claim commit**: `185026f`
- **Origin**: registered from the user's [SIEGE-27](SIEGE-27.md) review. On the 5m grid a storey is as tall as a cell is wide, so a 45° stair takes a whole cell (no floor before outside stairs, narrow stair-room turns bridged by NavMesh links, side-boarded corner stairs). The user proposed 7.5m × 7.5m cells with 5m storeys; this spike compared it before the Game migration (`SIEGE-18`).
- **Decision (user, 2026-11-23)**: move the Game to **7.5m cells with 5m storeys** and the **stretch** look. Recorded in [WallsAndSiege](../../design/WallsAndSiege.md) and [Decisions-Resolved](../../plan/Decisions-Resolved.md); `SIEGE-18` updated to match.
- **Scope** (spike only; Game untouched apart from `RunContent.asset`, below):
  - Separate scene `Assets/_Project/Scenes/SyntyGrid75Spike.unity` with the same castle as `SyntyGridSpike` on 7.5m cells (user choice: only the 7.5m castle in its own scene; the 5m spike stays). Key `L` switches stretch ↔ compose.
  - Cell width separated from storey height in the kit dressing; both looks; stair cases; Game impact list.
- **Implementation**:
  - `Walls/CastleKitLook.cs` (new): `Stretch = 0`, `Compose = 1`.
  - `Walls/CastleKitRules.cs`: no more uniform `cell ÷ module` scale; pieces keep module height, thickness and module-metre settings, and horizontal fit goes into `StretchX/StretchZ`. `FacePieces` (stretch one module along the face, or a module plus a half with a joint pillar/cap; `centred` for gate frames and doors; `keepModule` keeps gate frames unstretched in both looks), `FloorCell` (stretch or one full + three `FloorHalf`), joint dictionaries keyed on a 0.25m lattice. Outside stairs keep the 5m run (2.5m of walkway before the foot); `CornerStair` pivots one module back from the higher cell's edge. Breach halves, cross-section (`RubbleTop`, also across the wall thickness) and rubble pile stretch to the cell; `AddBreachWedge` adds a tilted `RoomCollider` under each V half. `Collect(..., kitLook)`.
  - `Walls/CastleKitPiece.cs`: `RoomPanelHalf = 32` (`Wall_Cut_01` with collider, for stair rooms and tower rooms). `CastleKitSettings` piece count 33.
  - `Walls/CastleKitDresser.cs`: `Look` property (rebuilds on change); pieces built at module scale.
  - `Walls/CastleKitTestKeys.cs`: `L` toggles the look.
  - `Editor/SyntyGridSpikeSetup.cs`: `Build(scene, data, cell, hint)` shared by `Create` (5m) and `Create75` (menu `ProjectNeo/Setup/Create 7.5m Grid Spike (SIEGE-35)`); piece heights always 5m; 7.5m kit room landing 2m and flight width 2.16m; camera ×1.5; `StairsPrefab` makes a 5m-wide centred stair copy (`Prefabs/SyntyGridSpike/StairsNarrow.prefab`, `Data/SyntyGrid75Spike/StairsRampNarrow.asset`); `Prefabs/CastleKit` is overwritten in place (GUIDs kept) so both scenes share it.
  - Tests: EditMode `CastleKitWideCellTests` (stretch, compose, centred gate in both looks, outside stair run, corner stair reach).
- **User review rounds (2026-11-23)**:
  1. Stretch looks better than compose, but the stretched gate looks wrong → gate frames keep the 5m module, centred (centre 3.75m) with 1.25m fillers.
  2. The player-built stair filling the 7.5m cell was too wide → 5m wide and centred (the climb still spans the cell, about 34°, to reach the wall); the breach cross-section kept 5m → now spans the 7.5m wall.
  3. The player could walk into the hollow of a V-breach half → wedge colliders; the ground gap in the middle (about 2.25m on 7.5m, 1.5m on 5m) stays open.
- **Automated Verification**:
  - Code rules check passed.
  - EditMode `838/838` PASSED (5m `CastleKitRulesTests` / `StairRoomRulesTests` / `TowerRulesTests` unchanged).
  - PlayMode `337/338`; the one failure is the known pre-existing `GameMoralePlayTests.ContagionTicksInScene_OnlyNearbyAlliesLoseMorale_AndHudShowsCheck` (expected 145, was 150; same as in [SIEGE-27](SIEGE-27.md)). Run before review rounds 2 and 3; those touched only spike dressing and the spike setup and were checked with EditMode and the in-editor checks below.
- **In-editor Play Mode checks** (`SyntyGrid75Spike`):
  - NavMesh paths complete in both looks from the courtyard to every south and north wall top, through outside straight and corner stairs and the three stair rooms (x 4, 7, 10); with every stair-room turn link disabled the paths stay the same, so the links are not needed on 7.5m cells.
  - The 2-storey / 1-storey corner / 3-storey case built at runtime ((3,4) raised) still connects.
  - Narrow player stair: collider 5.0 × 5.0 × 7.5m, path from the courtyard up to the north walkway complete.
  - Collapse of (12,6) in both spike scenes: a point inside the V half is blocked, the middle gap is clear, and the outside-to-courtyard path runs straight through the gap (7.5m: 37.5m path = direct distance; 5m: 25.0m).
  - No console errors.
- **Manual Verification**: the user played the 7.5m scene after each round (gate, stair width, breach section, breach hollow) and chose 7.5m stretch. Steps to repeat:
  - Open `Assets/_Project/Scenes/SyntyGrid75Spike.unity` (menu `ProjectNeo/Setup/Create 7.5m Grid Spike (SIEGE-35)` rebuilds it; if the walls show no kit after a rebuild, run the menu once more, see Known limits) and Play.
  - `L` switches stretch ↔ compose; the gate frame stays 5m and centred in both.
  - The courtyard stair west of the gate is 5m wide, centred, and climbable.
  - Climb the south row stair rooms (x 4, 7, 10) from the low walkway to the roof.
  - Aim at a one-storey wall and press `K`: the ruin spans the wall thickness, its sides and inside are solid, and the middle gap is walkable.
  - Reset: stop Play Mode; do not save the scene.
- **Game impact list** (not changed; input for `SIEGE-18`): 80×100 cells of 3m become about 32×40 cells of 7.5m and the 96m ring 13 cells (97.5m), with far fewer buildable cells; code that assumes storey height = cell size (`SiegeLadder` offsets, `MachicolationLayout`, `CastleFootprint`, `CastleBattlements`, camera, grid lines); 2×2 towers 15m (a 1×1 tower could hold the inner stair); bastion mount and cover sizes; ladder lanes and machicolations per 7.5m cell; spare room on weapon mounts; about three more defenders abreast per walkway cell; per-cell wall costs and HP for 1.5× wall length; camera ×1.5.
- **Known limits**: once, right after rebuilding the 7.5m scene, its `CastleKit.asset` loaded with every prefab reference empty (no kit in Play Mode); rebuilding again fixed it and the references then persisted on disk and after Play Mode. The cause was not found. `RunSaveSetup.Apply` added the 7.5m spike piece definitions to `Data/GameFlow/RunContent.asset`, as the 5m spike already does. Room doors and tower arrow slits stay stretched in the stretch look (user: keep as is).
- **Follow-ups**: `SIEGE-18` (Game migration to 7.5m stretch), `SIEGE-29` (towers reuse `StairWell`), `SIEGE-26` (enemy targeting of stair rooms).
