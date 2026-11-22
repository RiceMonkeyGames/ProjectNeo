# ART-06 | 2026-11-22 | Art - Synty Castle Wall Body & Stair Visuals

## Scope and decisions

- Branch: `feature/art-06`; youngho. Follows [ART-05](ART-05.md) (art pass on a shared prefab).
- User decisions (2026-11-22):
  - Apply the `PolygonFantasyKingdom` castle assets so that stacked wall tiers read as one wall.
  - After mockups, keep a natural look for the wall body and the stairs. Split the rule-changing work into `SIEGE-13`: Synty-proportion battlements, stepped sloped stair battlements, and a 6m main gate with `SM_Bld_Castle_Wall_Gate_01` plus two-tier default walls.
- Reasons recorded for the split:
  - The game uses 3m cells and 1.1m / 2.2m cover heights, with a crenel at each edge middle. The Synty kit uses 5m modules and 0.7m / 1.38m battlements with a merlon at the middle. Matching the Synty proportions changes gameplay cover, so it is a rule change.
  - `SM_Bld_Castle_Wall_04` pilasters break at every tier. `SM_Bld_Castle_Wall_Block_01` stacks seamlessly.
  - The pack has no sloped battlement asset.
- Rules and layout: [Walls & Siege design](../../design/WallsAndSiege.md).

## Implementation

- `Editor/CastleWallArtSetup` (menu `ProjectNeo/Setup/Apply Castle Wall Art`, idempotent):
  - Replaces the `Visual` children of `Prefabs/ConstructionPrototype/Wall.prefab` (`SM_Bld_Castle_Wall_Block_01`) and `Stairs.prefab` (`SM_Bld_Stairs_Straight_Wide_01`) with unlinked copies fitted to the 1x1x1 unit box. The stairs are yawed 90 degrees so the high side is +Z, like the `UnitRamp` collider.
  - Removes the model colliders. NavMesh uses `PhysicsColliders`, and the root colliders are unchanged.
  - Creates URP Lit copies in `Art/Castle/Materials` through `CharacterModelSetup.ToUrp`, which gained an optional folder parameter.
  - Battlements keep their shapes and colliders. `BattlementSettings.textureTileMeters` (0 = old builtin cube) makes `CastleBattlements` use the new `Walls/TiledBoxMesh` (unit box whose UVs are castle-space metres / tile, built per part and destroyed with the battlement). Castle-space UVs matter: parapet and merlon boxes, and the two faces meeting at a corner, overlap on the same plane; with per-box UVs the textures z-fought and flickered as the camera moved (found by the user in play, fixed). The setup sets the `Data/GameFlow/Build/Battlements.asset` material to the Synty stone URP copy and the tile size to 3.047m (3m cell / wall block UV span), so the stone size matches the walls.
- Shared prefabs: ConstructionPrototype, the Siege prototypes and Game all show the new visuals.
- Main gate: unchanged (placeholder) until `SIEGE-13`.
- Re-run `ProjectNeo/Setup/Apply Castle Wall Art` after regenerating these prefabs.
- Code: `Runtime/Walls/TiledBoxMesh`, `Runtime/Walls/BattlementSettings`, `Runtime/Walls/CastleBattlements`. See [WallsAndSiege codemap](../../codemap/WallsAndSiege.md).

## Verification

- Automated: compile passed; EditMode 690/690 (new `TiledBoxMeshTests`); PlayMode 300/300; `code-rules-check` passed.
- In-editor: Game scene play capture checked walls, stairs and battlements (Lv1 and Lv2 shapes).
- Manual play: confirmed by the user (2026-11-22) after the z-fighting fix. The user's first play also surfaced inner-face battlements after loading a save; that is a pre-existing SIEGE-10 rule issue (gapped ring), registered as `BUG-battlement-inner-faces`.
- Not verified: standalone build (not run).
