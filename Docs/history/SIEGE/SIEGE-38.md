# SIEGE-38 | 2026-11-28 | SIEGE - Uniform-Scale Wall Module Kit

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-28: SIEGE-38 - Uniform-Scale Wall Module Kit

- **Assignee**: `신용준` | **Branch**: `feature/siege-38` (merged with `feature/siege-40`) | **Claim commit**: `03515b3` | **Work commits**: `f1614e1`, `8da9c9c`
- **Origin**: basin map decision 2026-11-27 ([Decisions-Resolved](../../plan/Decisions-Resolved.md)): replace the SIEGE-35 1.5× stretch with kit pieces at their own size.
- **Measurement**: all 348 `PolygonFantasyKingdom/Prefabs/Castle` prefabs measured in the Editor: walls, battlements and floors on a 5m module (halves 2.5m, quarters 1.25m), 5m storeys, straight stairs 5m run for 5m rise, round towers made of `Wall_Corner_L` quarters (outer radius about 7.4m).
- **Decisions (user, 2026-11-27/28)**: 5m × 5m cells with 5m storeys, no scaling; players who want stair room build two-deep walls. Full kit-module audit split out to `SIEGE-41`.
- **Implementation**:
  - `Editor/Grid5Migration` (menu `Migrate Wall Grid To 5m Kit Size`): shared wall, stairs, bastion and gate definitions, prototype grids, cameras and scenes moved to 5m; the 5m `MainGate.prefab`. Prototype setups create 5m grids.
  - `Editor/UrpMaterialCopy`: URP copies of Synty materials now carry normal, emission, metallic/gloss and occlusion maps (menu `Refresh URP Material Copies From Synty Sources`); both converters use it.
  - `CastleKitDresser.closedSideCells` (+ `CastleKitRules`): no kit dressing toward grid end cells taken by other structures.
  - `ConstructionEditPreview` + `Editor/ConstructionEditPreviewDriver`: run-start walls drawn in edit mode from an unsaved copy (`ConstructionWorld.BuildEditPreview`).
  - Basin draft scene `Scenes/Preview/FantasyKingdomBasin.unity` (rotated Synty demo, north wall grid, end towers, foundation, field, rim mountains).
- **Verification**: compile passed; EditMode 1023/1023; PlayMode 385/394 (the 9 known failures: eye height, HUD/text, Game one-cell stairs). Captures by the agent of wall joins and towers; user checked the look in the editor.