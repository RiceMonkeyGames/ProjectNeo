---
id: ART-06
title: Synty Castle Wall Body & Stair Visuals
status: In Progress
assignee: youngho
branch: feature/art-06
area: ART
depends_on: []
created: 2026-11-22
---

# ART-06 - Synty Castle Wall Body & Stair Visuals

## Scope & Completion Criteria

User request 2026-11-22: apply `PolygonFantasyKingdom` castle assets to the wall pieces so stacked tiers read as one continuous wall. Visual-only (no rule, collider, size or data value changes):

- Wall piece `Visual` uses `SM_Bld_Castle_Wall_Block_01` (seamless when stacked 2-3 tiers; chosen over `SM_Bld_Castle_Wall_04`, whose pilasters break at every tier).
- Stairs piece `Visual` uses `SM_Bld_Stairs_Straight_Wide_01` (5x5x5m 45-degree stair, uniform fit to the 3m cell).
- URP Lit copies of the Synty materials under `Assets/_Project/Art/`; the third-party pack stays unmodified.
- Existing battlements keep their shape; only their material follows the new wall stone if needed for consistency.

Out of scope (split to `SIEGE-13`): Synty battlement meshes and proportions, stepped sloped battlements on stairs, 6m main gate with `SM_Bld_Castle_Wall_Gate_01`, default walls raised to two tiers.
