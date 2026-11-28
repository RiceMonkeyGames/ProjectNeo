---
id: SIEGE-42
title: Build Grid Visible Over Uneven Ground
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [SIEGE-40]
created: 2026-11-28
---

# SIEGE-42 - Build Grid Visible Over Uneven Ground

## Scope & Completion Criteria

Registered 2026-11-28 by the user: in the basin Game (`SIEGE-40`) the construction-view grid lines (`ConstructionWorld/GridLines`, shown by `GamePhaseView` in the construction view) sit at the flat build plane, but the visual field (`ApproachGround`, FK `SM_Generic_Ground_*` pieces) rises up to ~0.7m above that plane over the band, so the lines are buried and the cells cannot be read in construction mode.

- Make the grid readable over the bumpy ground in the construction view: e.g. draw the lines on top (no depth test / overlay pass), project them onto the visible ground, or lift them per cell to the highest visual surface. Check the hover cell highlight and placement preview too.
- Keep the gameplay plane unchanged (collision, NavMesh and placement stay on the flat slab).
- Check from the construction camera's default and max zoom, and at the end towers and gate.

Done when every cell of the band is clearly visible in construction mode on the basin Game scene, verified in play by the user.