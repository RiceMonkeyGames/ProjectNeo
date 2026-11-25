---
id: ART-09
title: Artisan Courtyard NPCs & Worksite Visuals
status: Waiting for Prerequisite
assignee: Unassigned
branch:
area: ART
depends_on: [PREP-11]
created: 2026-11-25
---

# ART-09 — Artisan Courtyard NPCs & Worksite Visuals

## Scope & Completion Criteria

Registered 2026-11-25 by the user as the visual follow-up to [PREP-11](../history/PREP/PREP-11.md) (design in [PrepEconomyMeta](../design/PrepEconomyMeta.md), [Decisions-Resolved](../plan/Decisions-Resolved.md)). Brings the abstract artisan headcount to life in the `Game` courtyard by building on the `SIEGE-36` `Town` resident system (`TownDirector`, `TownResident`, `TownRules`, `TownSettings`).

### Scope

1. **Artisan NPC Roster Sync**:
   - Spawn and maintain `N` distinct Artisan NPCs in `Game.unity` matching `RunState`'s current artisan headcount (spawning a new artisan near the Construction Station immediately when hired during preparation, and matching restored runs on Continue).
   - Use Synty `PolygonFantasyKingdom` character/prop visuals strictly under `Visual` / `Town` presentation objects (zero impact on build placement, colliders, combat targeting, or unit counts).

2. **Preparation Worksite & Idle Behavior**:
   - When construction or repair orders occupy active artisan slots in `GameConstructionWork`, assign one Artisan NPC per active order to walk via NavMesh to a reachable stand spot beside that structure or siege-weapon worksite (or the nearest safe wall/stair base if elevated/unreachable) and play a working/hammering loop animation.
   - Idle artisans without an active construction/repair slot gather near the Construction Station or stroll nearby courtyard paths.
   - Re-evaluate assignments cleanly when orders are prioritized/reordered, completed, or cancelled.

3. **Battle Shelter Integration**:
   - At battle start, Artisan NPCs retreat to the nearest shelter door and hide for the duration of battle alongside `TownDirector` citizens, then emerge again when the next preparation phase begins (`SIEGE-36` shelter rules).

### Open Before Start (Confirm with User)

- Synty `PolygonFantasyKingdom` model/tool prop selection for artisans (distinct from existing facility NPCs and roaming citizens) and working animation clip choice.

### Completion Criteria

- Pure C# EditMode tests cover artisan-to-order visual assignment mapping (hire/add, reorder, complete, cancel, fallback stand point).
- PlayMode verification in `Game` confirms `N` artisan NPCs spawn, walk to active build/repair worksites, update on hiring/cancellation, shelter indoors during battle without affecting `AlliesAlive` or combat targeting, and return on next preparation.
- `Docs/codemap/FlowRunEconomy.md` / `Docs/codemap/ArtAssets.md` updated and manual Play Mode walkthrough provided.
