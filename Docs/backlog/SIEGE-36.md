---
id: SIEGE-36
title: Larger Default Castle with Synty Courtyard
status: In Progress
assignee: 신용준
branch: feature/siege-36
area: SIEGE
depends_on: [SIEGE-18, SIEGE-19]
created: 2026-11-25
---

# SIEGE-36 - Larger Default Castle with Synty Courtyard

## Goal

The default castle given at a new run looks small and like a prototype. Double its width and depth and dress the courtyard with Synty assets.

## User decisions (2026-11-25)

- Ring 26 × 26 cells of 7.5m (195m), up from 13 × 13. The ground grows to keep the margins and the 120m north approach. Old saved runs are not migrated (start a new run).
- Courtyard facilities keep their colliders and interactions; their placeholder look is replaced by `PolygonFantasyKingdom` building models.
- Courtyard floor is paved with Synty stone; decorations (fountain, statue, trees, planters, flowers). Large decorations (fountain, statue, big trees) block movement; small ones (flowers, bushes) do not.
- Wall shape: one-storey ring, two-storey gatehouse, and two-storey corners; more ground stairs for the longer ring.

## Scope

- `Editor/GameFlowSetup`: grid, ring, seeds, courtyard, squads, player start, build camera.
- Ladders and spawn points follow the new north wall.
- Game PlayMode tests that use ring coordinates.
- Docs: design/WallsAndSiege, codemap, ArtAssets, history.
