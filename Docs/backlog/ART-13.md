---
id: ART-13
title: Outer Battlefield & Environment Art Pass
status: Waiting
assignee: Unassigned
branch:
area: ART
depends_on: []
created: 2026-11-26
---

# ART-13 — Outer Battlefield & Environment Art Pass

## Scope & Completion Criteria

Upgrade the northern enemy approach, perimeter terrain, and scene atmosphere beyond the initial `SIEGE-36` placeholder backdrop using approved environment packs (`PolygonFantasyKingdom`, `PolygonNature`, `PolygonNatureBiomes`; [ArtAssets](../codemap/ArtAssets.md), [Milestones](../plan/Milestones.md)).

### Planned Design & Scope

1. **Northern Approach & Perimeter Environment**:
   - Enrich the 120m northern siege approach and side flanks (`SIEGE-36` 345×397.5m map) with natural ground variation, rock outcrops, forest edge layering, and distant terrain landmarks around the northern spawn points (Left / Center / Right) so enemy waves emerge from coherent staging areas.
   - Keep all outer combat-lane decorations strictly non-colliding (or outside walkable lanes and spawn cells) so enemy pathfinding, ladder lanes, ranged LoS, and build-grid cells are completely unaffected.

2. **Lighting & Atmosphere**:
   - Polish directional light, skybox, distance fog, and shadow cascade tuning for readable contrast between dark enemies, castle stonework, and wall-top defenders across both first-person battle and orthographic construction cameras.

3. **Performance Guardrails**:
   - Use combined/batched static meshes or GPU instancing matching `SIEGE-36` courtyard setup; verify no regression in draw calls or frame time during combat.

### Open Before Start (Quick Confirm with User)

- Confirm environment biome/mood direction (e.g., overcast siege dawn vs clear midday) and whether `PolygonNature` / `PolygonNatureBiomes` assets are already imported in `Assets/ThirdParty`.

### Completion Criteria

- `Game` scene outer battlefield and lighting updated via repeatable Editor setup script (`Assets/ThirdParty` untouched).
- PlayMode verification confirms zero NavMesh regressions, clear spawn-point pathing for all 7 stages, and unchanged build-grid validity.
- `Docs/codemap/ArtAssets.md` updated with all applied environment asset paths and previews; manual visual inspection steps provided.
