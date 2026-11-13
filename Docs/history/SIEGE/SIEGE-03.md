# SIEGE-03 | 2026-11-13 | Construction - One-cell Full-tier Stairs

Last Updated: 2026-11-13 14:57:42 (KST)

## Scope and implementation

- Branch: `feature/siege-03`; Codex. Done after user confirmed stair feel and authorized completion on 2026-11-13. Task branch commit/push authorized; develop merge remains user-owned.
- Q-42: shared stair definition changed from 1×2 to 1×1 cells, retaining the full 3m rise. Existing normalized ramp mesh and collider scale through ConstructionWorld.SetPose to 3×3×3m. Shading remains tracked separately by ART-01.
- Editor API migration moved 7 initial stairs in 4 scenes (ConstructionPrototype, WallCommandPrototype, SiegePrototype, Game), retaining the high landing. Rotation 0 shifts one cell north, rotation 1 one east; rotations 2/3 retain the anchor. Setup generators use the corresponding new positions. Re-running migration returned `Already migrated; navigation slope verified.` without moving seeds again.
- Default Humanoid NavMesh slope increased from 45° to 46°. At 45°, all four new two-tier movement cases stopped on ground; at 46°, units completed both directions. Player movement at its former 45° limit failed north/south ascent; new PlayerControlSettings.MaximumWalkSlope defaults to 46° and is assigned in PlayerInputController.Awake. This is tolerance for the approved 45° geometry. Vertical walls remain disconnected when the only ground stair is destroyed.
- Unity documents slope-dependent exclusion and voxelization sensitivity in [NavMeshBuildSettings.agentSlope](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/ai/navmeshbuildsettings/agentslope). Full regression covers existing wall navigation and collapse behavior after the shared agent setting changes.
- No changes to CharacterActor, UnitCombatAI, ladder contracts, construction costs, materials, or build order. Serialized assets/scenes/settings were changed through Editor APIs only.

## Verification

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter OneCellStairs`: 4/4 passed, 3.84s.
- New `StackedConstructionPlayTests.OneCellStairs_PlayerAndUnitAscendAndDescendTwoTiers` cases 0–3: actual Game scene, two connected 3m rises, unit Move commands and PlayerInputController.Move in both directions; no player HP loss; stair occupancy prevents stacking; destroying the only ground stair makes the remaining wall-top path partial.
- Full compile and EditMode: passed, 216/216 (5.67s).
- First full PlayMode: 117/118 (51.88s). Only failure was the previous test expecting a rotated stair to occupy 2×1 cells. Updated `ConstructionPlayTests.Confirm_RotatesFootprint_RejectsOverlap_AndUpdatesNavigation` to require 1×1, retained rotation, and a free adjacent landing cell. Final full PlayMode rerun: **118/118 passed (53.74s)**, compile passed.
- Editor API audit rebuilt each initial pure layout and required every stair to have a landing: ConstructionPrototype 7 pieces/1 stair, WallCommandPrototype 7/1, SiegePrototype 14/2, Game 126/3; all valid. Original editor scene setup restored.
- User reported that the stair feel was good and authorized completion on 2026-11-13. This confirms user acceptance of the 45° stairs; the exact manual rotation/tier/squad matrix was not separately reported. Standalone build remains unverified.

## User feedback before completion

User asked whether 1:1 stairs are too steep. Explained that 3m rise over 3m run is 45°, versus about 26.6° for the old 1×2 ramp. Automated traversal succeeded. User subsequently confirmed that the feel was good and requested completion, accepting the approved Q-42 dimensions. Lower rise per cell would require agreeing on multi-piece/partial-tier support rules before changing implementation.

## Manual verification

1. Open `Assets/_Project/Scenes/Game.unity` and enter Play Mode (fresh Stage 1 preparation).
2. Walk to either north courtyard stair using WASD/mouse; ascend onto the wall and descend. Each ramp must occupy exactly one 3m grid cell, meet the 3m wall top, and work without jumping or HP loss. Repeat at the south stair.
3. Approach the construction station and press F. Select stairs and press R through all four orientations. Preview and placement must occupy one cell; adjacent cells remain available. Place the high end next to a wall. Esc returns to first-person; walk up/down each orientation.
4. For a two-tier route, build a ground stair into a wall-top walkway, put a stair on one supported wall cell, and build a two-high wall at that stair's high end. Both tiers must remain connected. In first-person, F1 selects squad 1; aim at the accessible wall top and press 1, then aim at ground and press 1. Units should ascend/descend rather than stop beside the wall. Player must also traverse both tiers.
5. For a simpler isolated demo, open `Assets/_Project/Scenes/WallCommandPrototype.unity`, Play, use `Move atop wall`, then Tab for first-person and walk the shortened stair. The squad must reach the wall top. `Destroy foundation` should retain normal fall behavior.
6. Stop Play Mode and reopen Game to reset temporary construction and unit positions. Automated repeat: Test Runner > PlayMode > filter `OneCellStairs` > Run Selected, or use the scoped wrapper command above.
