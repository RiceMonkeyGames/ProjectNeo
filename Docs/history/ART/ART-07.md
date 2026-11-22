# ART-07 | 2026-11-23 | Art - Dynamic Castle Wall Trim (Buttresses, Plinth, Corbels, Corner Turrets) (Superseded)

Status: **Superseded** without merge (2026-11-23, youngho). Branch `feature/art-07` has no commits beyond the claim; the work-in-progress box trim stays only in the local stash `ART-07 wall trim WIP (boxes)` on youngho's machine. Decision: [Decisions-Resolved](../../plan/Decisions-Resolved.md) ("Game adopts the 5m castle kit").

## Original scope

Registered 2026-11-22: generated relief on the 3m textured-box walls (buttress pilasters, a plinth, a corbel band under the parapet, corner turrets), visual only, outer faces only, rebuilt on every layout change.

## Why it closed

The team moved the Game to the 5m `PolygonFantasyKingdom` castle kit ([SIEGE-14](../SIEGE/SIEGE-14.md)). The kit dresser already generates the same kind of relief from the real kit pieces as walls are built and removed: `Wall_04` pilaster panels on outer faces, joint pillars, `Wall_Panel_Stone_02` relief on courtyard faces, and the tower corbel ring. Convex corner pieces remain open in `SIEGE-19`.
