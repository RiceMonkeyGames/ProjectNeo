---
id: BUG-curtain-navmesh-read
title: Game Scene PlayMode Tests Fail on Unreadable Curtain Mesh in Runtime NavMesh Build
status: In Progress
assignee: SeongminHwang
branch: fix/bug-curtain-navmesh-read
area: SIEGE
depends_on: [SIEGE-40]
created: 2026-11-28
---

# BUG-curtain-navmesh-read - Game Scene PlayMode Tests Fail on Unreadable Curtain Mesh

## Scope & Completion Criteria

Registered 2026-11-28 by the user, found while merging `PREP-12` into `develop` (`45aabe0`). Since the basin Game scene (`SIEGE-40`, `ddde4eb`), every PlayMode test that loads `Assets/_Project/Scenes/Game.unity` fails in `SetUp` with an unhandled error log:

`[Error] RuntimeNavMeshBuilder: Source mesh SM_Prop_Curtain_Large_04 does not allow read access. This will work in playmode in the editor but not in player`

Seen in `GameTavernPlayTests` (2/2) and `GameFacilityPlayTests` (13/13). EditMode is unaffected (1056/1056). `PREP-12` changed no scene or asset, so the error comes from the scene content.

Likely cause: the runtime NavMesh rebuild (`ConstructionWorld` with the `NavMeshSurface` wired by `Editor/GameBasinSetup`) collects render meshes from the scene geometry, and the Synty curtain prop placed in the demo town has Read/Write disabled. In a standalone player the bake would also miss or reject this mesh.

- Keep decorative props out of the NavMesh sources: put them outside the surface's collected hierarchy or on an excluded layer, use colliders/physics shapes as the source, or add a `NavMeshModifier` that ignores them. Prefer this over enabling Read/Write on licensed meshes in `Assets/ThirdParty` (that would move the submodule pointer).
- Check that no other prop in the basin scene raises the same error.
- Make the change through Editor APIs or the setup script (`Editor/GameBasinSetup`), never by hand-editing `.unity` / `.prefab` / `.meta`.

Done when the full PlayMode suite shows no `RuntimeNavMeshBuilder` read-access errors, `GameTavernPlayTests` and `GameFacilityPlayTests` pass again, units still path correctly around the demo town in play, and a standalone build log shows no NavMesh source warnings.
