# ART-01 | 2026-11-13 | Art - Flatten Placeholder Stair Shading

Last Updated: 2026-11-13 15:07:57 (KST)

## Scope and implementation

- Branch: `feature/art-01`; Claude. Visual-only change; stair footprint, height, collider shape and navigation are unchanged.
- Cause: `Art/Placeholder/ConstructionRamp.asset` (`UnitRamp`) shared 6 vertices across all faces, so `RecalculateNormals` averaged slope/side/back normals into a smooth gradient that did not match flat wall boxes.
- New `Editor/ConstructionRampMesh` builds the same 8 triangles with per-face vertices (18 vertices) and flat normals. `ConstructionPrototypeSetup.RampMesh` uses it for new assets. Menu `ProjectNeo/Setup/Flatten Stair Shading` (`ConstructionRampMesh.Flatten()`) rewrites the existing mesh asset in place via Editor APIs, keeping its GUID so the Stairs prefab Visual MeshFilter and root MeshCollider references stay intact.
- Applied once: `Flattened ramp: vertices=18, triangles=8.` Face normals: slope (0, 0.71, -0.71), left (-1,0,0), right (1,0,0), back (0,0,1), bottom (0,-1,0). Bounds unchanged (center (0,0.5,0), extents 0.5).

## Verification

- Compile passed; EditMode 216/216; PlayMode filter `Construction` 41/41 (includes stacked/one-cell stair traversal).
- Preview-scene render of the Stairs prefab with a directional light shows a single uniform tone per face (no gradient).
- Manual Game-view check by user pending.

## Manual verification

1. Open `Assets/_Project/Scenes/Game.unity`, enter Play Mode.
2. Look at the north/south courtyard stairs in first-person: slope and sides each show one flat tone like adjacent walls, no gradient from bottom to top.
3. Press F at the construction station, select stairs, rotate with R and place; check the placed stair in construction view and after Esc in first-person.
4. Walk up and down the stair once to confirm traversal is unchanged. Stop Play Mode to reset.
