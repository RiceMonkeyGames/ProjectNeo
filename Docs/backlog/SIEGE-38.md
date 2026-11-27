---
id: SIEGE-38
title: Uniform-Scale Wall Module Kit
status: In Progress
assignee: 신용준
branch: feature/siege-38
area: SIEGE
depends_on: []
created: 2026-11-27
---

# SIEGE-38 - Uniform-Scale Wall Module Kit

## Scope & Completion Criteria

Registered 2026-11-27 from the basin map decision ([Decisions-Resolved](../plan/Decisions-Resolved.md), [WallsAndSiege](../design/WallsAndSiege.md)). Replaces the SIEGE-35 1.5× stretch look, which looks unnatural.

- Measure the Synty `PolygonFantasyKingdom` castle kit pieces (wall panels, corners, battlements, gate frames, stairs, tower parts) in the Editor (do not read or edit `Assets/ThirdParty` files; measure through Editor APIs) and propose a base unit (the largest size that most pieces divide evenly). Ask the user to confirm the unit and the wall grid cell size (the unit or a whole multiple) before building.
- **Measured 2026-11-27** (all 348 `PolygonFantasyKingdom/Prefabs/Castle` prefabs, mesh bounds in prefab space): `Wall_01`–`05`, arrow slits, windows, doors 5.00×5.00m (0.5–1.1m thick); `Wall_Cut_*` 2.5/1.25m; `Battlements_01`–`06` 5.00m long, `_Half` 2.50m; floors 5/2.5/1.25m; `Wall_Gate_01` 5.67×5.86m (5m opening plus frame); corners L/M/S 5.25/4.00/2.75m; `Stairs_Straight_01` and `Stairs_Wood_Straight_01` about 5m run for 5m rise (45°), `Stairs_Straight_02` 2.5m rise; `Wall_Tower_L/M/S_01` 3.8/3.0/2.4m wide, 7.5m tall. **Decision (user)**: 5m cells, 5m storeys, no scaling (see [WallsAndSiege](../design/WallsAndSiege.md)).
- Build the module set with uniform scale only: pick fitting kit pieces, and where a size is missing make the piece from kit meshes (Blender or composed prefabs under `Assets/_Project`). No non-uniform scaling.
- Update the castle kit dressing and `CastleKitSettings` to the new unit; remove the stretch path. Stair, stair-room, breach and collapse sizes follow the new cell.

Done when every wall module in the dressing has scale (1,1,1) (or one uniform factor), the unit and cell size are recorded in the design docs, and the spike scene shows walls, corners, gate, stairs and towers without stretching.
