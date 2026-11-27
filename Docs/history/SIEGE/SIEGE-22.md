# SIEGE-22 | 2026-11-27 | SIEGE - Main Gate & Gatehouse on the 7.5m Grid

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-27: SIEGE-22 - Main Gate & Gatehouse on the 7.5m Grid

- **Assignee**: `youngho` | **Branch**: `feature/siege-22` | **Claim commit**: `492dd5b`
- **Origin**: registered 2026-11-23 from the move of the Game to the castle kit; source [SIEGE-14](SIEGE-14.md) breakage list item 6. Rescoped 2026-11-26 to the 7.5m cells with 5m storeys of [SIEGE-35](SIEGE-35.md) and [SIEGE-18](SIEGE-18.md). Kit dressing, gate stair rooms and the portcullis phase link were already done in [SIEGE-19](SIEGE-19.md).
- **Measurement (kit `SM_Bld_Castle_Wall_Gate_01`, raycast on the model)**: outer arch 4.26m wide with a 4.58m peak; the narrowest point is the door rebate, 2.92m wide (arch 3.32m at the sides, 3.93m at the centre); door leaves 3.85m tall, 0.33–0.78m inside the outer face; portcullis slot at the face. The old placeholder gate collider left a 5.25m × 4.1m opening with the door at the cell centre, so units could walk through the kit stone frame.
- **Decisions (user, 2026-11-27; recorded in [Decisions-Resolved](../../plan/Decisions-Resolved.md))**:
  - Gate colliders follow the kit frame through a new 7.5m gate prefab used by the 7.5m gate definitions; the old `MainGate.prefab` stays for the 5m spike scene.
  - Machicolations keep one opening per outward gate face; only sizes change.
- **Implementation**:
  - `Editor/KitGateSetup` (menu `ProjectNeo/Setup/Apply 7.5m Kit Gate (SIEGE-22)`) builds `Prefabs/ConstructionPrototype/MainGate75.prefab`: outer and inner frames (side blocks plus a lintel above 3.85m around a 2.92m opening, 0.75m deep), 0.31m passage side walls, a 0.2m ceiling slab, and the door collider (2.92 × 3.85m, 0.45m thick, centre 0.55m inside the outer face). Values are metres normalized by the 7.5 × 5m cell; `Visual/StoneArch` and `Visual/Door` boxes mirror them for scenes without the kit.
  - Assigned to `Data/ConstructionPrototype/Gate.asset` (Game and prototypes), `Data/SiegePrototype/Gate.asset` and `Data/SyntyGrid75Spike/Gate.asset`. `Grid75Migration.ConfigureGate`, `SiegePrototypeSetup` and the 7.5m branch of `SyntyGridSpikeSetup` use the new prefab.
  - `MachicolationSettings` defaults and `Data/GameFlow/Build/Machicolations.asset` (`MachicolationSetup.ApplySizes`, menu `ProjectNeo/Setup/Resize Machicolations For 7.5m Cells (SIEGE-22)`): shaft width 7.5m (whole gate face, since wall breakers hug it), inward 1.2m, outward 2m, search radius 15m, overhang 4.3 × 0.9 × 0.8m with the kit `Panel` stone material.
  - Docs: [design/WallsAndSiege](../../design/WallsAndSiege.md) machicolation 7.5m bullet, [codemap/WallsAndSiege](../../codemap/WallsAndSiege.md) kit gate paragraph.
- **Verification**:
  - Code rules: `VIOLATIONS=0`. Compile passed.
  - EditMode 1001/1001. PlayMode 385/394; the 9 failures (eye height 2, one-cell ground stairs 2, HUD/text 5) are the same set as the SIEGE-24 and SIEGE-30 baselines. The wrapper timed out at 180s and the run was polled to completion (282s).
  - Play Mode check in `Game.unity` (agent): gate colliders at the planned bounds; closed door gives a partial NavMesh path through the gate, held open gives a complete path with the doors swung inward, breach hides doors and portcullis and opens the path, repair closes it again; at battle start the portcullis lowers; gate breakers and a gate crusher reached the gate, broke it (300 HP) and entered the courtyard through the passage on the 26-cell ring.
  - Manual play (user, 2026-11-27): confirmed.
- **Manual verification**: play `Assets/_Project/Scenes/Game.unity`. Open and close the gate in preparation and walk through the passage (no snagging on the frame, no invisible walls); look at the gatehouse front from outside (machicolation overhang about 4.3m wide); press `전투 시작` (portcullis lowers; enemies break the gate and enter through the passage). Stop Play Mode to reset; no scene file changes.
- **Known limits**: the setup menus rebuild `MainGate75.prefab` each time they run (same path, GUID kept). The kit door rebate arch dips to 3.32m at its sides while the lintel collider starts at 3.85m, so a little of the arch corner is visual only. Prototype scenes without the kit show the box placeholder.
