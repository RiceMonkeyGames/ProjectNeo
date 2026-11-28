---
id: SIEGE-41
title: Castle Kit Module Audit
status: Waiting
assignee: Unassigned
branch:
area: SIEGE
depends_on: [SIEGE-38]
created: 2026-11-28
---

# SIEGE-41 - Castle Kit Module Audit

## Scope & Completion Criteria

Registered 2026-11-28 by the user: check every module of the 5m castle kit dressing (`CastleKitSettings` pieces, `CastleKitRules`) now that the grid uses the kit at its own size (`SIEGE-38`).

- For each module (wall panels and accents, half panels, floors, battlements and crenellations, caps and pillars, corner turrets, gate frame and portcullis, outside and corner stairs, stair rooms, tower storeys and roofs, breach and collapse pieces, rubble): uniform scale, correct pivot and seams against neighbours, no z-fighting or gaps, materials with all maps (`UrpMaterialCopy`), colliders that match the look.
- Note anything that still relies on the old stretch path or on `battlementHeightScale` (non-uniform Lv1 battlement height from SIEGE-20) and agree a fix with the user and the teammate who owns combat cover.
- Capture each module in the `SyntyGridSpike` scene (5m) for the user to review; list fixes as follow-up tasks or fix small ones here.

Done when every module is checked with the user, the findings are recorded here or in history, and agreed fixes are done or registered.