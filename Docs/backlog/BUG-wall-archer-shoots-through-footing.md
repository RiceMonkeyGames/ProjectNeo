---
id: BUG-wall-archer-shoots-through-footing
title: Wall-Top Archers Shoot Through Their Own Wall Piece
status: Waiting
assignee: Unassigned
branch:
area: BUG
depends_on: []
created: 2026-11-21
---

# BUG-wall-archer-shoots-through-footing — Wall-Top Archers Shoot Through Their Own Wall Piece

## Scope & Completion Criteria

Registered 2026-11-21 from the user's COMBAT-09 playtest: allied archers on the wall right above the gate hit enemies breaking the gate by shooting straight down through the wall. Cause: the shooter's own footing piece (`CombatPhysics.GroundUnder`) is ignored by both the line-of-sight check (`HasClearShot`) and `CombatProjectile` (rule from BUG-wall-archer-los, [Combat](../design/Combat.md) Line of sight). Open before start: how much of the footing to ignore (only near the feet, or only shots that leave past the outer wall edge), and whether steep downward shots are blocked or later allowed through a dedicated feature (for example machicolations). Shared combat core (`CombatPhysics`, `CombatProjectile`): coordinate with the teammate.
